using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Represents a set of config values associated
    /// with a peer in the chatroom
    /// </summary>
    public class VoiceChatPeerConfig {
        /// <summary>
        /// Whether this peer is muted
        /// </summary>
        public bool muteIncoming = false;

        /// <summary>
        /// Whether this peer will receive out voice
        /// </summary>
        public bool muteOutgoing = false;

        /// <summary>
        /// The <see cref="AudioSource"/> from where the audio coming from this peer will be played. 
        /// </summary>
        public AudioSource audioSource;
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

        public Func<short, int, int, AudioSource, IAudioOutput> AudioOutputProvider { get; set; }

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
        /// <see cref="VoiceChatPeerConfig"/> associated with each entry in <see cref="Peers"/>
        /// </summary>
        public Dictionary<short, VoiceChatPeerConfig> PeerConfigs;     

        /// <summary>
        /// Creates a new agent using a signalling server URL
        /// </summary>
        /// <param name="signallingServer"></param>
        /// <returns></returns>
        public VoiceChatAgent(IChatroomNetwork network, IAudioInput input) {
            AudioInput = input;
            Network = network;
            PeerConfigs = new Dictionary<short, VoiceChatPeerConfig>();
            AudioOutputs = new Dictionary<short, IAudioOutput>();

            Init();
        }

        /// <summary>
        /// Disposes the internal network and resets the instance state
        /// </summary>
        public void Dispose() {
            Network.Dispose();
            PeerConfigs.Clear();
            AudioOutputs.Clear();
            Mute = false;
        }

        void Init() {
            // Node server events
            Network.OnChatroomCreated += () => CurrentMode = VoiceChatAgentMode.Host;
            Network.OnChatroomClosed += () => {
                CurrentMode = VoiceChatAgentMode.Unconnected;
                PeerConfigs.Keys.ToList().ForEach(x => RemovePeer(x));
            };

            // Node client events
            Network.OnJoined += id => {
                CurrentMode = VoiceChatAgentMode.Guest;
                EnsurePeerConfig(0);
            };
            Network.OnLeft += () => PeerConfigs.Keys.ToList().ForEach(x => RemovePeer(x));
            Network.OnPeerJoined += id => EnsurePeerConfig(id);
            Network.OnPeerLeft += id => RemovePeer(id);

            // Client data events
            // On receiving a message from a peer,
            // read the audio data and play it on the 
            // right streamer if we're not muting the peer
            Network.OnAudioReceived += (id, index, frequency, channels, data) => {
                EnsurePeerStreamer(id, frequency, channels);
                if (PeerConfigs.ContainsKey(id) && !PeerConfigs[id].muteIncoming)
                    AudioOutputs[id].Stream(index, data);
            };

            // When an audio sample from the mic is ready,
            // package and send it to all the peers that we 
            // are not muting ourselves to.
            AudioInput.OnSegmentReady += (index, samples) => {
                if (Mute) return;

                var recipients = Network.Peers.Where(x => PeerConfigs.ContainsKey(x) && !PeerConfigs[x].muteOutgoing);

                foreach (var recipient in recipients)
                    Network.SendAudioSegment(recipient, index, AudioInput.Frequency, AudioInput.ChannelCount, samples);
            };
        }

        void RemovePeer(short id) {
            if (PeerConfigs.ContainsKey(id)) {
                MonoBehaviour.Destroy(PeerConfigs[id].audioSource.gameObject);
                PeerConfigs.Remove(id);
            }
            if (AudioOutputs.ContainsKey(id)) {
                AudioOutputs[id].Dispose();
                AudioOutputs.Remove(id);
            }
        }

        void EnsurePeerConfig(short id) {
            if (!PeerConfigs.ContainsKey(id)) {
                var audioSource = new GameObject($"Peer #{id}").AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                var config = new VoiceChatPeerConfig { audioSource = audioSource };
                PeerConfigs.Add(id, config);
            }
        }

        void EnsurePeerStreamer(short id, int frequency, int channels) {
            if (!AudioOutputs.ContainsKey(id) && PeerConfigs.ContainsKey(id)) {
                var output = AudioOutputProvider?.Invoke(id, frequency, channels, PeerConfigs[id].audioSource);
                AudioOutputs.Add(id, output);
            }
        }
    }
}
