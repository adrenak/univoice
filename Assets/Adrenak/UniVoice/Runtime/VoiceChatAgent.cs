using System;
using System.Linq;
using System.Collections.Generic;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Provides the means to host or connect to a chatroom using concrete 
    /// implementations of <see cref="IChatroomNetwork"/>, 
    /// <see cref="IAudioInput"/> and <see cref="IAudioOutputFactory"/>
    /// </summary>
    public class VoiceChatAgent : IDisposable {
        // ================================================
        // DEPENDENCIES
        // ================================================
        /// <summary>
        /// The underlying network which the agent uses to connect to 
        /// chatrooms and send and receive data to and from peers
        /// </summary>
        public IChatroomNetwork ChatroomNetwork { get; private set; }

        /// <summary>
        /// Source of audio input that can be 
        /// transmitted over the network to peers
        /// </summary>
        public IAudioInput AudioInput { get; private set; }

        /// <summary>
        /// A "provider" that returns an <see cref="IAudioOutput"/> 
        /// instance every time a Peer connects for that peer.
        /// </summary>
        public IAudioOutputFactory AudioOutputFactory { get; private set; }

        // ================================================
        // STATE
        // ================================================                                                            
        /// <summary>
        /// Responsible for playing the audio that we receive from peers. 
        /// There is a <see cref="IAudioOutput"/> for each peer that gets
        /// created using the provided <see cref="AudioOutputFactory"/>
        /// </summary>
        public Dictionary<short, IAudioOutput> PeerOutputs;

        /// <summary>
        /// The current <see cref="VoiceChatAgentMode"/> of this agent
        /// </summary>
        public VoiceChatAgentMode CurrentMode { get; private set; }

        /// <summary>
        /// Whether this agent is muted or not. If set to true, voice data will
        /// not be sent to ANY peer.  If you want to selectively mute yourself 
        /// to peers, use the <see cref="VoiceChatPeerSettings.muteOutgoing"/> 
        /// flag in the <see cref="PeerSettings"/> instance for that peer.
        /// </summary>
        public bool Mute { get; set; }

        /// <summary>
        /// <see cref="VoiceChatPeerSettings"/> for each peer
        /// </summary>
        public Dictionary<short, VoiceChatPeerSettings> PeerSettings;

        /// <summary>
        /// Creates and returns a new agent using the provided dependencies.
        /// </summary>
        /// 
        /// <param name="chatroomNetwork">The chatroom network implementation for 
        /// accessing chatrooms and sending data to peers
        /// </param>
        /// 
        /// <param name="audioInput">The source of the outgoing audio</param>
        /// 
        /// <param name="audioOutputFactory">
        /// The factory used for creating and destroying
        /// <see cref="IAudioOutput"/> instances for peers
        /// </param>
        public VoiceChatAgent(
            IChatroomNetwork chatroomNetwork,
            IAudioInput audioInput,
            IAudioOutputFactory audioOutputFactory
        ) {
            AudioInput = audioInput ?? 
            throw new ArgumentNullException(nameof(audioInput));

            ChatroomNetwork = chatroomNetwork ?? 
            throw new ArgumentNullException(nameof(chatroomNetwork));
            
            AudioOutputFactory = audioOutputFactory ?? 
            throw new ArgumentNullException(nameof(audioOutputFactory));

            CurrentMode = VoiceChatAgentMode.Unconnected;
            Mute = false;
            PeerSettings = new Dictionary<short, VoiceChatPeerSettings>();
            PeerOutputs = new Dictionary<short, IAudioOutput>();

            Init();
        }

        /// <summary>
        /// Disposes the internal network and resets state
        /// </summary>
        public void Dispose() {
            RemoveAllPeers();
            PeerSettings.Clear();
            PeerOutputs.Clear();
            ChatroomNetwork.Dispose();
            AudioInput.Dispose();
        }

        void Init() {
            // Node server events
            ChatroomNetwork.OnChatroomCreated += () =>
                CurrentMode = VoiceChatAgentMode.Host;
            ChatroomNetwork.OnChatroomClosed += () => {
                CurrentMode = VoiceChatAgentMode.Unconnected;
                RemoveAllPeers();
            };

            // Node client events
            ChatroomNetwork.OnJoined += id => {
                CurrentMode = VoiceChatAgentMode.Guest;
                EnsurePeerSettings(0);
            };
            ChatroomNetwork.OnLeft += () =>
                RemoveAllPeers();
            ChatroomNetwork.OnPeerJoined += id =>
                EnsurePeerSettings(id);
            ChatroomNetwork.OnPeerLeft += id =>
                RemovePeer(id);

            // Stream the incoming audio data using the right peer output
            ChatroomNetwork.OnAudioReceived += data => {
                var id = data.id;
                var index = data.segmentIndex;
                var frequency = data.frequency;
                var channels = data.channelCount;
                var samples = data.samples;

                EnsurePeerStreamer(id, frequency, channels, samples.Length);

                if (HasSettingsForPeer(id) && !PeerSettings[id].muteIncoming)
                    PeerOutputs[id].Feed(index, frequency, channels, samples);
            };


            AudioInput.OnSegmentReady += (index, samples) => {
                if (Mute) return;

                // Get all the recipients we haven't muted ourselves to
                var recipients = ChatroomNetwork.PeerIDs
                    .Where(x => {
                        return HasSettingsForPeer(x)
                        && !PeerSettings[x].muteOutgoing;
                    });

                // Send the audio segment to every deserving recipient
                foreach (var recipient in recipients)
                    ChatroomNetwork.SendAudioSegment(new ChatroomAudioDTO {
                        id = recipient,
                        segmentIndex = index,
                        frequency = AudioInput.Frequency,
                        channelCount = AudioInput.ChannelCount,
                        samples = samples
                    });
            };
        }

        void RemovePeer(short id) {
            if (PeerSettings.ContainsKey(id))
                PeerSettings.Remove(id);
            if (PeerOutputs.ContainsKey(id)) {
                AudioOutputFactory.Destroy(PeerOutputs[id]);
                PeerOutputs[id].Dispose();
                PeerOutputs.Remove(id);
            }
        }

        void RemoveAllPeers() =>
            PeerSettings.Keys.ToList().ForEach(x => RemovePeer(x));

        void EnsurePeerSettings(short id) =>
            PeerSettings.EnsureKey(id, new VoiceChatPeerSettings());

        bool HasSettingsForPeer(short id) => PeerSettings.ContainsKey(id);

        void EnsurePeerStreamer
        (short id, int frequency, int channels, int segmentLength) {
            if (!PeerOutputs.ContainsKey(id) && PeerSettings.ContainsKey(id)) {
                var output = AudioOutputFactory.Create(
                    frequency,
                    channels,
                    segmentLength
                );
                output.ID = id.ToString();
                PeerOutputs.Add(id, output);
            }
        }
    }
}
