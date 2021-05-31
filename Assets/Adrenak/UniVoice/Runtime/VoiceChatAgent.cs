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
        /// The sampling frequency at which the Mic should operate.
        /// .
        /// Must be constant and the same for all clients. Different values on different clients is NOT supported.
        /// </summary>
        const int MIC_FREQUENCY = 16000;

        /// <summary>
        /// The number of times the audio samples should be read from the mic. The length of the segment
        /// is 1000 / <see cref="SEGMENTS_PER_SEC"/> milliseconds. So, if set to 10, each segment will have audio 
        /// samples worth 1000 / 10 = 100 milliseconds.
        /// .
        /// Must be constant and the same for all clients. Different values on different clients is NOT supported.
        /// </summary>
        const int SEGMENTS_PER_SEC = 10;

        /// <summary>
        /// <para>
        /// The number of audio segments the <see cref="AudioBuffer"/> for each client should hold. 
        /// See <see cref="AudioBuffer"/> to see what this class does
        /// </para>
        /// 
        /// <para>
        /// -- Equations --
        /// The duration of the buffer is 1000 / <see cref="SEGMENTS_PER_SEC"/> * <see cref="BUFFER_SEGMENT_COUNT"/>.
        /// So if:
        /// <see cref="SEGMENTS_PER_SEC"/> = 10
        /// <see cref="BUFFER_SEGMENT_COUNT"/> = 5
        /// Then the buffer duration would 500 milliseconds.
        /// </para>
        /// 
        /// <para>
        /// INFERENCE
        /// The value of 500 ms derived above means the buffer holds upto 500 milliseconds of audio. The 500 ms
        /// is then made available to <see cref="AudioStreamer"/> as internal latency that can be used to fix
        /// issues in audio reception caused due to network irregularities at runtime.
        /// 
        /// A value of 500 ms also means that at any given time, network error can cause the internal latency to
        /// go up to 500 ms. So, don't set this value too high.
        /// </para>
        /// 
        /// <para>
        /// Set the value to one that corresponds to a buffer duration that greated than the fluctuation you expect 
        /// in network latency. So if you expect the network latency to be 100 to 300 ms. Set this to a value
        /// that will result in a buffer duration of 200 ms or more.
        /// </para>
        /// .
        /// Must be constant and the same for all clients. Different values on different clients is NOT supported.
        /// </summary>
        const int BUFFER_SEGMENT_COUNT = 5;

        /// <summary>
        /// The minimum number of segments the <see cref="AudioBuffer"/> should have for the <see cref="AudioStreamer"/> 
        /// to play the audio. 
        /// .
        /// -- Equations --
        /// 1000 / <see cref="SEGMENTS_PER_SEC"/> * <see cref="BUFFER_SEGMENT_COUNT"/> * <see cref="STREAMER_MIN_SEGMENT_COUNT"/>
        /// is the duration of audio the <see cref="AudioBuffer"/> should have filled ahead of time for the <see cref="AudioStreamer"/>
        /// to play. 
        /// .
        /// In case of network problems, the streamer uses up to the maximum latency (as determined by <see cref="BUFFER_SEGMENT_COUNT"/>
        /// to ensure uninterrupted audio playback.
        /// .
        /// -- Inference -- 
        /// When set to a low value, network latency fluctiations and out of order audio reception may cause gaps 
        /// in the audio playback as we don't allow the buffer to fill up enough to eliminate the gaps ahead of time.
        /// .
        /// When set to a high value, the buffer is allowed to fill up and ensure that gaps are filled despite network 
        /// latency fluctuations, but the minimum internal latency goes up.
        /// .
        /// -- Range --
        /// Maximum value usable is <see cref="BUFFER_SEGMENT_COUNT"/>, at which all the available 
        /// internal latency (as determined by <see cref="BUFFER_SEGMENT_COUNT"/>) will be used.
        /// .
        /// Minimum value is 1, but the network needs to have a perfectly constant latency for there to be no gaps.
        /// Expecting such conditions are not practical.
        /// .
        /// Suggested value: Set to one that corresponds to a minimum internal latency greater than the average 
        /// fluctuation you expect  in network latency. So if you expect the network latency to be 100 to 300 ms. 
        /// Set to a value that will result in an internal latency of (300 - 100) / 2 = 100 ms or more.
        /// .
        /// Must be constant and the same for all clients. Different values on different clients is NOT supported.
        /// </summary>
        const int STREAMER_MIN_SEGMENT_COUNT = 3;

        /// <summary>
        /// Fired when a chatroom is successfully created
        /// </summary>
        public event Action OnCreateChatroom;

        /// <summary>
        /// Fired when a chatroom creation fails
        /// </summary>
        public event Action<Exception> OnCouldNotCreeateChatroom;

        /// <summary>
        /// Fired when the chatroom is shutdown
        /// </summary>
        public event Action OnShutdownChatroom;

        /// <summary>
        /// Fired when this node successfully joins a chatroom and 
        /// is assigned an ID
        /// </summary>
        public event Action<short> OnJoined;

        /// <summary>
        /// Fired when this peer successfully leaves a chatroom
        /// </summary>
        public event Action OnLeft;

        /// <summary>
        /// Fired when a peer (another user) enters the chatroom
        /// </summary>
        public event Action<short> OnPeerJoined;

        /// <summary>
        /// Fired when a peer (another user) leaves the chatroom
        /// </summary>
        public event Action<short> OnPeerLeft;

        /// <summary>
        /// Fired when this instance is disposed
        /// </summary>
        public event Action OnDispose;

        /// <summary>
        /// Fired when we receive audio from a peer
        /// </summary>
        public event Action<short, int, float[]> OnGetAudio;

        /// <summary>
        /// Fired when we send audio to a peer
        /// </summary>
        public event Action<short[], int, float[]> OnSendAudio;

        /// <summary>
        /// The current <see cref="Mode"/> of this agent
        /// </summary>
        public Mode MyMode { get; private set; } = Mode.Unconnected;

        /// <summary>
        /// ID this agent has been assigned in the current chatroom.
        /// Returns -1 if the agent is not in a chatroom.
        /// </summary>
        public short ID => node != null ? node.ID : (short)-1;

        /// <summary>
        /// That name of the chatroom this agent is connected to on hosting
        /// </summary>
        public string ChatRoomName => node != null ? node.Address : null;

        /// <summary>
        /// Whether this agent is muted or not
        /// </summary>
        public bool Mute { get; set; } = false;

        /// <summary>
        /// The other peers in the same chatroom as this agent
        /// </summary>
        public List<short> Peers => ID == -1 ? new List<short>() : node.Peers;

        /// <summary>
        /// <see cref="PeerConfig"/> associated with each entry in <see cref="Peers"/>
        /// </summary>
        public Dictionary<short, PeerConfig> PeerConfigs;

        /// <summary>
        /// List of <see cref="IAudioOperation"/> that are applied to the outgoing audio
        /// </summary>
        public List<IAudioOperation> Filters { get; private set; }

        /// <summary>
        /// List of <see cref="IAudioGate"/> that are used to control the outgoing audio
        /// </summary>
        public List<IAudioGate> Gates { get; private set; }

        APNode node;
        Dictionary<short, AudioStreamer> streamers;

        // Prevent creating instance using 'new' keyword
        VoiceChatAgent() { }

        /// <summary>
        /// Creates a new agent using a signalling server URL
        /// </summary>
        /// <param name="signallingServer"></param>
        /// <returns></returns>
        public static VoiceChatAgent New(string signallingServer) {
            var go = new GameObject("UniVoice");
            DontDestroyOnLoad(go);
            var cted = go.AddComponent<VoiceChatAgent>();

            cted.node = new APNode(signallingServer);
            cted.Gates = new List<IAudioGate>();
            cted.Filters = new List<IAudioOperation>();
            cted.PeerConfigs = new Dictionary<short, PeerConfig>();
            cted.streamers = new Dictionary<short, AudioStreamer>();

            cted.Init();
            return cted;
        }

        /// <summary>
        /// Creates a voice chat room using a name
        /// </summary>
        /// <param name="roomName">The name to be used to identify the room.</param>
        public void CreateChatroom(string roomName) =>
            node.StartServer(roomName);

        /// <summary>
        /// Joins an existing voice chat room using name.
        /// </summary>
        /// <param name="roomName">The name of the room to be joined</param>
        public void JoinChatroom(string roomName) =>
            node.Connect(roomName);

        /// <summary>
        /// Leaves the voice chat room
        /// </summary>
        public void LeaveChatroom() {
            if (node.CurrentMode == APNode.Mode.Client)
                node.Disconnect();
        }

        /// <summary>
        /// Shuts down the chatroom (all the peers will get disconnected)
        /// </summary>
        public void ShutdownChatroom() {
            if (node.CurrentMode == APNode.Mode.Server)
                node.StopServer();
        }

        /// <summary>
        /// Disposes the internal network and resets the instance state
        /// </summary>
        public void Dispose() {
            node.Dispose();
            PeerConfigs.Clear();
            streamers.Clear();
            Mute = false;
        }

        void Init() {
            var mic = Mic.Instance;
            mic.StartRecording(MIC_FREQUENCY, 1000 / SEGMENTS_PER_SEC);

            // Node server events
            node.OnServerStartSuccess += () => {
                MyMode = Mode.Host;
                OnCreateChatroom?.Invoke();
            };
            node.OnServerStartFailure += ex => {
                OnCouldNotCreeateChatroom?.Invoke(ex);
            };
            node.OnServerStop += () => {
                MyMode = Mode.Unconnected;
                PeerConfigs.Keys.ToList().ForEach(x => RemovePeer(x));
                OnShutdownChatroom?.Invoke();
            };

            // Node client events
            node.OnConnected += () => {
                MyMode = Mode.Guest;
                EnsurePeerConfig(0);
            };
            node.OnReceiveID += id => {
                MyMode = Mode.Unconnected;
                OnJoined?.Invoke(id);
            };
            node.OnDisconnected += () => {
                PeerConfigs.Keys.ToList().ForEach(x => RemovePeer(x));
                OnLeft?.Invoke();
            };
            node.OnClientJoined += id => {
                EnsurePeerConfig(id);
                OnPeerJoined?.Invoke(id);
            };
            node.OnClientLeft += id => {
                RemovePeer(id);
                OnPeerLeft?.Invoke(id);
            };

            // Client data events
            // On receiving a message from a peer,
            // read the audio data and play it on the 
            // right streamer if we're not muting the peer
            node.OnPacketReceived += (id, packet) => {
                if (packet.Tag.Equals("audio")) {
                    var reader = new BytesReader(packet.Payload);
                    var index = reader.ReadInt();
                    var channels = reader.ReadInt();
                    EnsurePeerStreamer(id, channels);

                    if (PeerConfigs.ContainsKey(id) && !PeerConfigs[id].muteIncoming) {
                        var data = reader.ReadFloatArray();
                        streamers[id].Stream(index, data);
                        OnGetAudio?.Invoke(id, index, data);
                    }
                }
            };

            // When an audio sample from the mic is ready,
            // package and send it to all the peers that we 
            // are not muting ourselves to.
            mic.OnSampleReady += (index, segment) => {
                if (ID == -1 && Mute) return;

                foreach (var gate in Gates)
                    if (!gate.Evaluate(segment))
                        return;

                foreach (var filter in Filters)
                    segment = filter.Execute(segment);

                var packet = new Packet().WithTag("audio")
                    .WithPayload(new BytesWriter()
                        .WriteInt(index)
                        .WriteInt(mic.AudioClip.channels)
                        .WriteFloatArray(segment)
                        .Bytes
                    );

                var recipients = node.Peers
                    .Where(x => PeerConfigs.ContainsKey(x) && !PeerConfigs[x].muteOutgoing);

                if (recipients != null && recipients.Count() > 0) {
                    node.SendPacket(recipients.ToList(), packet, false);
                    OnSendAudio?.Invoke(recipients.ToArray(), index, segment);
                }
            };
        }

        void RemovePeer(short id) {
            if (PeerConfigs.ContainsKey(id)) {
                Destroy(PeerConfigs[id].audioSource.gameObject);
                PeerConfigs.Remove(id);
            }
            if (streamers.ContainsKey(id)) {
                Destroy(streamers[id].gameObject);
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

        void EnsurePeerStreamer(short id, int channels) {
            if (!streamers.ContainsKey(id) && PeerConfigs.ContainsKey(id)) {
                var segDataLen = MIC_FREQUENCY / SEGMENTS_PER_SEC;
                var segCount = BUFFER_SEGMENT_COUNT;
                var streamer = AudioStreamer.New(
                    new AudioBuffer(MIC_FREQUENCY, channels, segDataLen, segCount, $"Peer #{id} Clip"),
                    PeerConfigs[id].audioSource,
                    STREAMER_MIN_SEGMENT_COUNT
                );
                streamer.transform.SetParent(transform);
                streamers.Add(id, streamer);
            }
        }
    }
}
