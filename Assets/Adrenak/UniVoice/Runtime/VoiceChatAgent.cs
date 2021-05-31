using System;
using System.Linq;
using System.Collections.Generic;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Represents settings associated with a peer in the chatroom
    /// </summary>
    public class VoiceChatPeerAudioSettings {
        /// <summary>
        /// Whether this peer is muted. Use this to ignore a person.
        /// </summary>
        public bool muteIncoming = false;

        /// <summary>
        /// Whether this peer will receive out voice. Use this to keep
        /// say something without a person hearing.
        /// </summary>
        public bool muteOutgoing = false;
    }

    /// <summary>
    /// Represents the mode that the <see cref="VoiceChatAgent"/>
    /// instance is operating in.
    /// </summary>
    public enum VoiceChatAgentMode {
        Unconnected,
        Host,
        Guest
    }

    /// <summary>
    /// Provides the means to host or connect to a chatroom. Internally it uses an AirPeer APNode.
    /// </summary>
    public class VoiceChatAgent : IDisposable {

        /// <summary>
        /// Fired when this instance is disposed
        /// </summary>
        public event Action OnDispose;

        public IAudioInput AudioInput { get; set; }

        public Func<short, int, int, IAudioOutput> AudioOutputProvider { get; set; }

        public Dictionary<short, IAudioOutput> AudioOutputs;

        public IChatroomNetwork Network { get; private set; }

        /// <summary>
        /// The current <see cref="VoiceChatAgentMode"/> of this agent
        /// </summary>
        public VoiceChatAgentMode CurrentMode { get; private set; } = VoiceChatAgentMode.Unconnected;

        /// <summary>
        /// ID this agent has been assigned in the current chatroom.
        /// Returns -1 if the agent is not in a chatroom.
        /// </summary>
        public short ID => Network.ID;

        /// <summary>
        /// That name of the chatroom this agent is connected to on hosting
        /// </summary>
        public string ChatRoomName => Network?.CurrentRoomName;

        /// <summary>
        /// Whether this agent is muted or not
        /// </summary>
        public bool Mute { get; set; } = false;

        /// <summary>
        /// The other peers in the same chatroom as this agent
        /// </summary>
        public List<short> Peers => Network?.Peers;

        /// <summary>
        /// <see cref="VoiceChatPeerAudioSettings"/> associated with each entry in <see cref="Peers"/>
        /// </summary>
        public Dictionary<short, VoiceChatPeerAudioSettings> PeerAudioSettings;     

        /// <summary>
        /// Creates a new agent using a signalling server URL
        /// </summary>
        /// <param name="signallingServer"></param>
        /// <returns></returns>
        public VoiceChatAgent(IChatroomNetwork network, IAudioInput input) {
            AudioInput = input;
            Network = network;
            PeerAudioSettings = new Dictionary<short, VoiceChatPeerAudioSettings>();
            AudioOutputs = new Dictionary<short, IAudioOutput>();

            Init();
        }

        /// <summary>
        /// Disposes the internal network and resets the instance state
        /// </summary>
        public void Dispose() {
            Network.Dispose();
            PeerAudioSettings.Clear();
            AudioOutputs.Clear();
            Mute = false;
        }

        void Init() {
            // Node server events
            Network.OnChatroomCreated += () => CurrentMode = VoiceChatAgentMode.Host;
            Network.OnChatroomClosed += () => {
                CurrentMode = VoiceChatAgentMode.Unconnected;
                PeerAudioSettings.Keys.ToList().ForEach(x => RemovePeer(x));
            };

            // Node client events
            Network.OnJoined += id => {
                CurrentMode = VoiceChatAgentMode.Guest;
                PeerAudioSettings.EnsureKey((short)0, new VoiceChatPeerAudioSettings());
            };
            Network.OnLeft += () => PeerAudioSettings.Keys.ToList().ForEach(x => RemovePeer(x));
            Network.OnPeerJoined += id => PeerAudioSettings.EnsureKey(id, new VoiceChatPeerAudioSettings());
            Network.OnPeerLeft += id => RemovePeer(id);

            // Client data events
            // On receiving a message from a peer,
            // read the audio data and play it on the 
            // right streamer if we're not muting the peer
            Network.OnAudioReceived += (id, index, frequency, channels, data) => {
                EnsurePeerStreamer(id, frequency, channels);
                if (PeerAudioSettings.ContainsKey(id) && !PeerAudioSettings[id].muteIncoming)
                    AudioOutputs[id].Stream(index, data);
            };

            // When an audio sample from the mic is ready,
            // package and send it to all the peers that we 
            // are not muting ourselves to.
            AudioInput.OnSegmentReady += (index, samples) => {
                if (Mute) return;

                var recipients = Network.Peers.Where(x => PeerAudioSettings.ContainsKey(x) && !PeerAudioSettings[x].muteOutgoing);

                foreach (var recipient in recipients)
                    Network.SendAudioSegment(recipient, index, AudioInput.Frequency, AudioInput.ChannelCount, samples);
            };
        }

        void RemovePeer(short id) {
            if (PeerAudioSettings.ContainsKey(id)) 
                PeerAudioSettings.Remove(id);           
            if (AudioOutputs.ContainsKey(id)) {
                AudioOutputs[id].Dispose();
                AudioOutputs.Remove(id);
            }
        }

        void EnsurePeerStreamer(short id, int frequency, int channels) {
            if (!AudioOutputs.ContainsKey(id) && PeerAudioSettings.ContainsKey(id)) {
                var output = AudioOutputProvider?.Invoke(id, frequency, channels);
                AudioOutputs.Add(id, output);
            }
        }
    }
}
