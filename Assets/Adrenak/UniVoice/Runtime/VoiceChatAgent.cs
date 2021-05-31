using System;
using System.Linq;
using System.Collections.Generic;

using Adrenak.AirPeer;
using Adrenak.UniMic;

using UnityEngine;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Provides the means to host or connect to a chatroom. Internally it uses an AirPeer APNode.
    /// </summary>
    public class VoiceChatAgent : MonoBehaviour, IDisposable {
        /// <summary>
        /// Represents a set of config values associated
        /// with a peer in the chatroom
        /// </summary>
        public class PeerConfig {
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
        public enum Mode {
            Unconnected,
            Host,
            Guest
        }

        /// <summary>
        /// Fired when this instance is disposed
        /// </summary>
        public event Action OnDispose;

        public Func<short, int, int, AudioSource, IAudioOutput> AudioOutputProvider { get; set; }

        public IAudioInput AudioInput { get; set; }

        /// <summary>
        /// The current <see cref="Mode"/> of this agent
        /// </summary>
        public Mode MyMode { get; private set; } = Mode.Unconnected;

        /// <summary>
        /// ID this agent has been assigned in the current chatroom.
        /// Returns -1 if the agent is not in a chatroom.
        /// </summary>
        public short ID => Network != null ? Network.ID : (short)-1;

        /// <summary>
        /// That name of the chatroom this agent is connected to on hosting
        /// </summary>
        public string ChatRoomName => Network != null ? Network.CurrentRoomName : null;

        /// <summary>
        /// Whether this agent is muted or not
        /// </summary>
        public bool Mute { get; set; } = false;

        /// <summary>
        /// The other peers in the same chatroom as this agent
        /// </summary>
        public List<short> Peers => ID == -1 ? new List<short>() : Network.Peers;

        /// <summary>
        /// <see cref="PeerConfig"/> associated with each entry in <see cref="Peers"/>
        /// </summary>
        public Dictionary<short, PeerConfig> PeerConfigs;

        Dictionary<short, IAudioOutput> streamers;

        // Prevent creating instance using 'new' keyword
        VoiceChatAgent() { }

        public IChatroomNetwork Network { get; private set; }

        /// <summary>
        /// Creates a new agent using a signalling server URL
        /// </summary>
        /// <param name="signallingServer"></param>
        /// <returns></returns>
        public static VoiceChatAgent New(IChatroomNetwork network, IAudioInput input) {
            var go = new GameObject("UniVoice");
            DontDestroyOnLoad(go);
            var cted = go.AddComponent<VoiceChatAgent>();

            cted.AudioInput = input;
            cted.Network = network;
            cted.PeerConfigs = new Dictionary<short, PeerConfig>();
            cted.streamers = new Dictionary<short, IAudioOutput>();

            cted.Init();
            return cted;
        }

        /// <summary>
        /// Disposes the internal network and resets the instance state
        /// </summary>
        public void Dispose() {
            Network.Dispose();
            PeerConfigs.Clear();
            streamers.Clear();
            Mute = false;
        }

        void Init() {
            // Node server events
            Network.OnChatroomCreated += () => {
                MyMode = Mode.Host;
            };
            Network.OnChatroomClosed += () => {
                MyMode = Mode.Unconnected;
                PeerConfigs.Keys.ToList().ForEach(x => RemovePeer(x));
            };

            // Node client events
            Network.OnJoined += id => {
                MyMode = Mode.Guest;
                EnsurePeerConfig(0);
            };
            Network.OnLeft += () => {
                PeerConfigs.Keys.ToList().ForEach(x => RemovePeer(x));
            };
            Network.OnPeerJoined += id => {
                EnsurePeerConfig(id);
            };
            Network.OnPeerLeft += id => {
                RemovePeer(id);
            };

            // Client data events
            // On receiving a message from a peer,
            // read the audio data and play it on the 
            // right streamer if we're not muting the peer
            Network.OnAudioReceived += (id, index, frequency, channels, data) => {
                EnsurePeerStreamer(id, frequency, channels);
                if (PeerConfigs.ContainsKey(id) && !PeerConfigs[id].muteIncoming)
                    streamers[id].Stream(index, data);
            };

            // When an audio sample from the mic is ready,
            // package and send it to all the peers that we 
            // are not muting ourselves to.
            AudioInput.OnSegmentReady += (index, samples) => {
                if (ID == -1 || Mute) return;

                var recipients = Peers.Where(x => PeerConfigs.ContainsKey(x) && !PeerConfigs[x].muteOutgoing);

                foreach (var recipient in recipients)
                    Network.SendAudioSegment(recipient, index, AudioInput.Frequency, AudioInput.ChannelCount, samples);
            };
        }

        void RemovePeer(short id) {
            if (PeerConfigs.ContainsKey(id)) {
                Destroy(PeerConfigs[id].audioSource.gameObject);
                PeerConfigs.Remove(id);
            }
            if (streamers.ContainsKey(id)) {
                streamers[id].Dispose();
                streamers.Remove(id);
            }
        }

        void EnsurePeerConfig(short id) {
            if (!PeerConfigs.ContainsKey(id)) {
                var audioSource = new GameObject($"Peer #{id}").AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.transform.SetParent(transform);
                var config = new PeerConfig { audioSource = audioSource };
                PeerConfigs.Add(id, config);
            }
        }

        void EnsurePeerStreamer(short id, int frequency, int channels) {
            if (!streamers.ContainsKey(id) && PeerConfigs.ContainsKey(id)) {
                var output = AudioOutputProvider?.Invoke(id, frequency, channels, PeerConfigs[id].audioSource);
                streamers.Add(id, output);
            }
        }
    }
}
