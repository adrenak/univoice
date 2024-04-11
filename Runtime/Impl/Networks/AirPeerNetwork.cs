#if UNIVOICE_AIRPEER_NETWORK
using System;
using System.Collections.Generic;
using System.Linq;

using Adrenak.BRW;
using Adrenak.AirPeer;

using Debug = UnityEngine.Debug;

namespace Adrenak.UniVoice.Networks {
    /// <summary>
    /// A <see cref="IChatroomNetwork"/> implementation using AirPeer
    /// For more on AirPeer, visit https://www.vatsalambastha.com/airpeer
    /// 
    /// Notes:
    /// An APNode node doesn't receive its client ID immediately after 
    /// connecting to an APNetwork, it receives it after joining the network
    /// from the host. But while it's waiting it still has peers. 
    /// This class makes sure that until the APNode doesn't receive its ID,
    /// a consumer of it will think it hasn't been connected.
    /// 
    /// TLDR; APNode first connects to host, and is given its ID by the host 
    /// after joining. We don't let anyone know we have connected until th
    /// </summary>
    public class AirPeerNetwork : IAudioNetwork {
        const string TAG = "UniVoiceAirPeerNetwork";

        public event Action OnCreatedChatroom;
        public event Action<Exception> OnChatroomCreationFailed;
        public event Action OnClosedChatroom;

        public event Action<int> OnJoinedChatroom;
        public event Action<Exception> OnChatroomJoinFailed;
        public event Action OnLeftChatroom; 

        public event Action<int> OnPeerJoinedChatroom;
        public event Action<int> OnPeerLeftChatroom;

        public event Action<int, AudioFrame> OnAudioReceived;
        public event Action<int, AudioFrame> OnAudioSent;

        public int OwnID => node.ID;

        public List<int> PeerIDs =>
            OwnID != -1 ? node.Peers.Select(x => (int)x).ToList() : new List<int>();

        public string CurrentChatroomName => OwnID != -1 ? node.Address : null;

        readonly APNode node;

        /// <summary>
        /// Creates an AirPeer based chatroom network 
        /// </summary>
        /// <param name="signalingServerURL">The signaling server URL</param>
        /// <param name="iceServerURLs">ICE server urls</param>
        public AirPeerNetwork(string signalingServerURL, params string[] iceServerURLs) {
            Debug.unityLogger.Log(TAG, "Creating with signalling server URL and ICE server urls");
            node = new APNode(signalingServerURL, iceServerURLs);
            Init();
        }

        /// <summary>
        /// Creates an AirPeer based chatroom network
        /// </summary>
        /// <param name="signalingServerURL">The signaling server URL</param>
        public AirPeerNetwork(string signalingServerURL) {
            Debug.unityLogger.Log(TAG, "Creating with signalling server URL and default ICE server urls");
            node = new APNode(signalingServerURL);
            Init();
        }

        void Init() {
            node.OnServerStartSuccess += () => {
                Debug.unityLogger.Log(TAG, "Airpeer Server started.");
                OnCreatedChatroom?.Invoke();
            };
            node.OnServerStartFailure += e => {
                Debug.unityLogger.Log(TAG, "Airpeer Server start failed.");
                OnChatroomCreationFailed?.Invoke(e);
            };
            node.OnServerStop += () => {
                Debug.unityLogger.Log(TAG, "Airpeer Server stopped.");
                OnClosedChatroom?.Invoke();
            };

            node.OnConnectionFailed += ex => {
                Debug.unityLogger.Log(TAG, "Airpeer connection failed. " + ex);
                OnChatroomJoinFailed?.Invoke(ex);
            };

            // Think of this like "OnConnectionSuccess"
            node.OnReceiveID += id => {
                // If ID is not 0, this means we're a guest, not the host
                if (id != 0) {
                    Debug.unityLogger.Log(TAG, "Received Airpeer connection ID: " + id);
                    OnJoinedChatroom?.Invoke(id);

                    // The server with ID 0 is considered a peer immediately
                    OnPeerJoinedChatroom?.Invoke(0);
                }
            };
            node.OnDisconnected += () => {
                Debug.unityLogger.Log(TAG, "Disconnected from server");
                OnLeftChatroom?.Invoke();
            };
            node.OnRemoteServerClosed += () => {
                Debug.unityLogger.Log(TAG, "Airpeer server closed");
                OnLeftChatroom?.Invoke();
            };

            node.OnClientJoined += id => {
                Debug.unityLogger.Log(TAG, "New Airpeer peer joined: " + id);
                OnPeerJoinedChatroom?.Invoke(id);
            };
            node.OnClientLeft += id => {
                Debug.unityLogger.Log(TAG, "Airpeer peer left: " + id);
                OnPeerLeftChatroom?.Invoke(id);
            };

            node.OnPacketReceived += (sender, packet) => {
                // "audio" tag is used for sending audio data
                if (packet.Tag.Equals("audio")) {
                    var reader = new BytesReader(packet.Payload);
                    // The order we read the bytes in is important here.
                    // See SendAudioSegment where the audio packet is constructed.
                    var timestamp = reader.ReadLong();
                    var frequency = reader.ReadInt();
                    var channels = reader.ReadInt();
                    var samples = reader.ReadByteArray();

                    OnAudioReceived?.Invoke(sender, new AudioFrame {
                        timestamp = timestamp,
                        frequency = frequency,
                        channelCount = channels,
                        samples = samples
                    });
                }
            };
        }

        public void HostChatroom(object chatroomName = null) =>
            node.StartServer(Convert.ToString(chatroomName));

        public void CloseChatroom(object data = null) =>
            node.StopServer();

        public void JoinChatroom(object chatroomName = null) =>
            node.Connect(Convert.ToString(chatroomName));

        public void LeaveChatroom(object data = null) =>
            node.Disconnect();

        public void SendAudioSegment(int peerID, AudioFrame data) {
            if (OwnID == -1) return;

            long timestamp = data.timestamp;
            int frequency = data.frequency;
            int channelCount = data.channelCount;
            byte[] samples = data.samples;

            // Create an airpeer packet with tag "audio", that's the tag used to determine
            // on the receiving end for parsing audio data.
            var payload = new BytesWriter()
                .WriteLong(timestamp)
                .WriteInt(frequency)
                .WriteInt(channelCount)
                .WriteByteArray(samples);
            var packet = new Packet().WithTag("audio").WithPayload(payload.Bytes);

            node.SendPacket((short)peerID, packet, false);
            OnAudioSent?.Invoke(peerID, data);
        }

        public void Dispose() => node.Dispose();
    }
}
#endif