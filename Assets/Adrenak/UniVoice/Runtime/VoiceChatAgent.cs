using System;
using System.Linq;
using System.Collections.Generic;

namespace Adrenak.UniVoice {
    public interface IAudioOutputFactory {
        IAudioOutput Create(short peerID, int samplingRate, int channelCount);
        void Destroy(IAudioOutput audioOutput);
    }

    public class OutputLifecycle {
        public Func<short, int, int, IAudioOutput> OnCreate { get; private set; }
        public Action<IAudioOutput> OnDestroy { get; private set; }

        public OutputLifecycle(Func<short, int, int, IAudioOutput> onCreate, Action<IAudioOutput> onDestroy) {
            OnCreate = onCreate ?? throw new ArgumentNullException(nameof(onCreate));
            OnDestroy = onDestroy ?? throw new ArgumentNullException(nameof(onDestroy));
        }
    }

    /// <summary>
    /// Provides the means to host or connect to a chatroom using concrete implementations
    /// of <see cref="IChatroomNetwork"/>, <see cref="IAudioInput"/> and <see cref="IAudioOutput"/>
    /// </summary>
    public class VoiceChatAgent : IDisposable {
        /// <summary>
        /// Source of audio input that can be transmitted over the network to peers
        /// </summary>
        public IAudioInput AudioInput { get; set; }

        /// <summary>
        /// A "provider" that returns an <see cref="IAudioOutput"/> instance every time
        /// a Peer connects for that peer.
        /// </summary>
        // public Func<short, int, int, IAudioOutput> PeerOutputProvider { get; set; }

        public IAudioOutputFactory AudioOutputFactory { get; private set; }

        /// <summary>
        /// Responsible for playing the audio that we receive from peers. There is a 
        /// <see cref="IAudioOutput"/> for each peer. The <see cref="IAudioOutput"/>
        /// are populated using <see cref="PeerOutputProvider"/> which can be configured.
        /// </summary>
        public Dictionary<short, IAudioOutput> PeerOutputs;

        /// <summary>
        /// The underlying network which the agent uses to connect to chatrooms and 
        /// send and receive data to and from peers
        /// </summary>
        public IChatroomNetwork Network { get; private set; }

        /// <summary>
        /// The current mode of this agent described by <see cref="VoiceChatAgentMode"/>
        /// </summary>
        public VoiceChatAgentMode CurrentMode { get; private set; }

        /// <summary>
        /// Whether this agent is muted or not. If set to true, voice data will not be
        /// sent to ANY peer. If you want to selectively mute yourself to peers, use
        /// the <see cref="VoiceChatPeerSettings.muteOutgoing"/> flag in 
        /// <see cref="PeerSettings"/>
        /// </summary>
        public bool Mute { get; set; } = false;

        /// <summary>
        /// <see cref="VoiceChatPeerSettings"/> associated with each entry in <see cref="Peers"/>
        /// </summary>
        public Dictionary<short, VoiceChatPeerSettings> PeerSettings;

        /// <summary>
        /// Creates a new agent using the provided <see cref="IChatroomNetwork"/> and 
        /// <see cref="IAudioInput"/> implementations.
        /// </summary>
        /// <param name="network">The network for accessing chatrooms and sending data to peers</param>
        /// <param name="input">The source of the local user's audio</param>
        /// <returns></returns>
        public VoiceChatAgent(IChatroomNetwork network, IAudioInput input, IAudioOutputFactory peerOutputLifecycle) {
            AudioInput = input ?? throw new ArgumentNullException(nameof(input));
            Network = network ?? throw new ArgumentNullException(nameof(network));
            AudioOutputFactory = peerOutputLifecycle ?? throw new ArgumentNullException(nameof(peerOutputLifecycle));

            CurrentMode = VoiceChatAgentMode.Unconnected;
            Mute = false;
            PeerSettings = new Dictionary<short, VoiceChatPeerSettings>();
            PeerOutputs = new Dictionary<short, IAudioOutput>();

            InitializeListeners();
        }

        /// <summary>
        /// Disposes the internal network and resets the instance state
        /// </summary>
        public void Dispose() {
            RemoveAllPeers();
            PeerSettings.Clear();
            PeerOutputs.Clear();
            Mute = false;
            Network.Dispose();
            AudioInput.Dispose();
        }

        void InitializeListeners() {
            // Node server events
            Network.OnChatroomCreated += () => CurrentMode = VoiceChatAgentMode.Host;
            Network.OnChatroomClosed += () => {
                CurrentMode = VoiceChatAgentMode.Unconnected;
                RemoveAllPeers();
            };

            // Node client events
            Network.OnJoined += id => {
                CurrentMode = VoiceChatAgentMode.Guest;
                EnsurePeerSettings(0);
            };
            Network.OnLeft += () => RemoveAllPeers();
            Network.OnPeerJoined += id => EnsurePeerSettings(id);
            Network.OnPeerLeft += id => RemovePeer(id);

            // Stream the incoming audio data using the right peer output
            Network.OnAudioReceived += data => {
                var id = data.id;
                var index = data.segmentIndex;
                var frequency = data.frequency;
                var channels = data.channelCount;
                var samples = data.samples;

                EnsurePeerStreamer(id, frequency, channels);

                if (HasSettingsForPeer(id) && !PeerSettings[id].muteIncoming)
                    PeerOutputs[id].Feed(index, frequency, channels, samples);
            };


            AudioInput.OnSegmentReady += (index, samples) => {
                if (Mute) return;

                // Get all the recipients we haven't muted ourselves to
                var recipients = Network.PeerIDs
                    .Where(x => HasSettingsForPeer(x) && !PeerSettings[x].muteOutgoing);

                // Send the audio segment to every deserving recipient
                foreach (var recipient in recipients)
                    Network.SendAudioSegment(new ChatroomAudioDTO {
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

        void RemoveAllPeers() {
            PeerSettings.Keys.ToList().ForEach(x => RemovePeer(x));
        }

        void EnsurePeerSettings(short id) => PeerSettings.EnsureKey(id, new VoiceChatPeerSettings());

        bool HasSettingsForPeer(short id) => PeerSettings.ContainsKey(id);

        void EnsurePeerStreamer(short id, int frequency, int channels) {
            if (!PeerOutputs.ContainsKey(id) && PeerSettings.ContainsKey(id)) {
                var output = AudioOutputFactory.Create(id, frequency, channels);
                PeerOutputs.Add(id, output);
            }
        }
    }
}
