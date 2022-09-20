using System;
using System.Linq;
using System.Collections.Generic;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Provides the means to host or connect to a chatroom.
    /// </summary>
    public class ChatroomAgent : IDisposable {
        // ====================================================================
        #region PROPERTIES
        // ====================================================================
        /// <summary>
        /// The underlying network which the agent uses to host or connect to 
        /// chatrooms, and send and receive data to and from peers
        /// </summary>
        public IChatroomNetwork Network { get; private set; }

        /// <summary>
        /// Source of outgoing audio that can be 
        /// transmitted over the network to peers
        /// </summary>
        public IAudioInput AudioInput { get; private set; }

        /// <summary>
        /// A factory that returns an <see cref="IAudioOutput"/> 
        /// instance. Used every time a Peer connects for that peer to get
        /// an output for that peer.
        /// </summary>
        public IAudioOutputFactory AudioOutputFactory { get; private set; }

        /// <summary>
        /// There is a <see cref="IAudioOutput"/> for each peer that gets
        /// created using the provided <see cref="AudioOutputFactory"/>
        /// The <see cref="IAudioOutput"/> instance corresponding to a peer is
        /// responsible for playing the audio that we receive that peer. 
        /// </summary>
        public Dictionary<short, IAudioOutput> PeerOutputs;

        /// <summary>
        /// The current <see cref="ChatroomAgentMode"/> of this agent
        /// </summary>
        public ChatroomAgentMode CurrentMode { get; private set; }

        /// <summary>
        /// Mutes all the peers. If set to true, no incoming audio from other 
        /// peers will be played. If you want to selectively mute a peer, use
        /// the <see cref="ChatroomPeerSettings.muteThem"/> flag in the 
        /// <see cref="PeerSettings"/> instance for that peer.
        /// Note that setting this will not change <see cref="PeerSettings"/>
        /// </summary>
        public bool MuteOthers { get; set; }

        /// <summary>
        /// Whether this agent is muted or not. If set to true, voice data will
        /// not be sent to ANY peer. If you want to selectively mute yourself 
        /// to a peer, use the <see cref="ChatroomPeerSettings.muteSelf"/> 
        /// flag in the <see cref="PeerSettings"/> instance for that peer.
        /// Note that setting this will not change <see cref="PeerSettings"/>
        /// </summary>
        public bool MuteSelf { get; set; }

        /// <summary>
        /// <see cref="ChatroomPeerSettings"/> for each peer which allows you
        /// to read or change the settings for a specific peer. Use [id] to get
        /// settings for a peer with ID id;
        /// </summary>
        public Dictionary<short, ChatroomPeerSettings> PeerSettings;
        #endregion

        // ====================================================================
        #region CONSTRUCTION / DISPOSAL
        // ====================================================================
        /// <summary>
        /// Creates and returns a new agent using the provided dependencies.
        /// </summary>
        /// 
        /// <param name="chatroomNetwork">The chatroom network implementation
        /// for chatroom access and sending data to peers in a chatroom.
        /// </param>
        /// 
        /// <param name="audioInput">The source of the outgoing audio</param>
        /// 
        /// <param name="audioOutputFactory">
        /// The factory used for creating <see cref="IAudioOutput"/> instances 
        /// for peers so that incoming audio from peers can be played.
        /// </param>
        public ChatroomAgent(
            IChatroomNetwork chatroomNetwork,
            IAudioInput audioInput,
            IAudioOutputFactory audioOutputFactory
        ) {
            AudioInput = audioInput ??
            throw new ArgumentNullException(nameof(audioInput));

            Network = chatroomNetwork ??
            throw new ArgumentNullException(nameof(chatroomNetwork));

            AudioOutputFactory = audioOutputFactory ??
            throw new ArgumentNullException(nameof(audioOutputFactory));

            CurrentMode = ChatroomAgentMode.Unconnected;
            MuteOthers = false;
            MuteSelf = false;
            PeerSettings = new Dictionary<short, ChatroomPeerSettings>();
            PeerOutputs = new Dictionary<short, IAudioOutput>();

            Init();
        }

        /// <summary>
        /// Disposes the instance. WARNING: Calling this method will
        /// also dispose the dependencies passed to it in the constructor.
        /// Be mindful of this if you're sharing dependencies between multiple
        /// instances and/or using them outside this instance.
        /// </summary>
        public void Dispose() {
            AudioInput.Dispose();

            RemoveAllPeers();
            PeerSettings.Clear();
            PeerOutputs.Clear();

            Network.Dispose();
        }
        #endregion

        // ====================================================================
        #region INTERNAL 
        // ====================================================================
        void Init() {
            // Node server events
            Network.OnCreatedChatroom += () =>
                CurrentMode = ChatroomAgentMode.Host;
            Network.OnClosedChatroom += () => {
                CurrentMode = ChatroomAgentMode.Unconnected;
                RemoveAllPeers();
            };

            // Node client events
            Network.OnJoinedChatroom += id => {
                CurrentMode = ChatroomAgentMode.Guest;
                EnsurePeerSettings(0);
            };
            Network.OnLeftChatroom += () =>
                RemoveAllPeers();
            Network.OnPeerJoinedChatroom += id =>
                EnsurePeerSettings(id);
            Network.OnPeerLeftChatroom += id =>
                RemovePeer(id);

            // Stream the incoming audio data using the right peer output
            Network.OnAudioReceived += (peerID, data) => {
                // if we're muting all, no point continuing.
                if (MuteOthers) return;

                var index = data.segmentIndex;
                var frequency = data.frequency;
                var channels = data.channelCount;
                var samples = data.samples;

                EnsurePeerStreamer(peerID, frequency, channels, samples.Length);

                if (HasSettingsForPeer(peerID) && !PeerSettings[peerID].muteThem)
                    PeerOutputs[peerID].Feed(index, frequency, channels, samples);
            };

            AudioInput.OnSegmentReady += (index, samples) => {
                if (MuteSelf) return;

                // Get all the recipients we haven't muted ourselves to
                var recipients = Network.PeerIDs
                    .Where(x => {
                        return HasSettingsForPeer(x)
                        && !PeerSettings[x].muteSelf;
                    });

                // Send the audio segment to every deserving recipient
                foreach (var recipient in recipients)
                    Network.SendAudioSegment(recipient, new ChatroomAudioSegment {
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
                PeerOutputs[id].Dispose();
                PeerOutputs[id].Dispose();
                PeerOutputs.Remove(id);
            }
        }

        void RemoveAllPeers() =>
            PeerSettings.Keys.ToList().ForEach(x => RemovePeer(x));

        void EnsurePeerSettings(short id) =>
            PeerSettings.EnsureKey(id, new ChatroomPeerSettings());

        bool HasSettingsForPeer(short id) => PeerSettings.ContainsKey(id);

        void EnsurePeerStreamer(
            short id,
            int frequency,
            int channels,
            int segmentLength
        ) {
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
        #endregion
    }
}
