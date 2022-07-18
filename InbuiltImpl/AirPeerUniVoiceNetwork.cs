using System;
using System.Collections.Generic;

using Adrenak.AirPeer;

namespace Adrenak.UniVoice.InbuiltImplementations {
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
    public class AirPeerUniVoiceNetwork : IChatroomNetwork {
        public event Action OnCreatedChatroom;
        public event Action<Exception> OnChatroomCreationFailed;
        public event Action OnClosedChatroom;

        public event Action<short> OnJoinedChatroom;
        public event Action<Exception> OnChatroomJoinFailed;
        public event Action OnLeftChatroom;

        public event Action<short> OnPeerJoinedChatroom;
        public event Action<short> OnPeerLeftChatroom;

        public event Action<ChatroomAudioDTO> OnAudioReceived;
        public event Action<ChatroomAudioDTO> OnAudioSent;

        public short OwnID => node.ID;

        public List<short> PeerIDs =>
            OwnID != -1 ? node.Peers : new List<short>();

        public string CurrentChatroomName => OwnID != -1 ? node.Address : null;

        readonly APNode node;

        /// <summary>
        /// Creates an AirPeer based chatroom network 
        /// </summary>
        /// <param name="signalingServerURL">The signaling server URL</param>
        /// <param name="iceServerURLs">ICE server urls</param>
        public AirPeerUniVoiceNetwork
        (string signalingServerURL, string iceServerURLs) {
            node = new APNode(signalingServerURL, iceServerURLs);
            Init();
        }

        /// <summary>
        /// Creates an AirPeer based chatroom network
        /// </summary>
        /// <param name="signalingServerURL">The signaling server URL</param>
        public AirPeerUniVoiceNetwork(string signalingServerURL) {
            node = new APNode(signalingServerURL);
            Init();
        }

        void Init() {
            node.OnServerStartSuccess += () => OnCreatedChatroom?.Invoke();
            node.OnServerStartFailure += e =>
                OnChatroomCreationFailed?.Invoke(e);
            node.OnServerStop += () => OnClosedChatroom?.Invoke();

            node.OnConnectionFailed += ex => OnChatroomJoinFailed?.Invoke(ex);
            node.OnReceiveID += id => {
                if (id != 0) {
                    OnJoinedChatroom?.Invoke(id);
                    
                    OnPeerJoinedChatroom?.Invoke(0); // server joins instantly
                }
            };
            node.OnDisconnected += () => OnLeftChatroom?.Invoke();
            node.OnRemoteServerClosed += () => OnLeftChatroom?.Invoke();

            node.OnClientJoined += id => OnPeerJoinedChatroom?.Invoke(id);
            node.OnClientLeft += id => OnPeerLeftChatroom?.Invoke(id);

            node.OnPacketReceived += (sender, packet) => {
                if (packet.Tag.Equals("audio")) {
                    var reader = new BytesReader(packet.Payload);
                    var index = reader.ReadInt();
                    var frequency = reader.ReadInt();
                    var channels = reader.ReadInt();
                    var samples = reader.ReadFloatArray();

                    OnAudioReceived?.Invoke(new ChatroomAudioDTO {
                        id = sender,
                        segmentIndex = index,
                        frequency = frequency,
                        channelCount = channels,
                        samples = samples
                    });
                }
            };
        }

        public void HostChatroom(string chatroomName) =>
            node.StartServer(chatroomName);

        public void CloseChatroom() =>
            node.StopServer();

        public void JoinChatroom(string chatroomName) =>
            node.Connect(chatroomName);

        public void LeaveChatroom() =>
            node.Disconnect();

        public void SendAudioSegment(ChatroomAudioDTO data) {
            if (OwnID == -1) return;

            var recipientID = data.id;
            var segmentIndex = data.segmentIndex;
            var frequency = data.frequency;
            var channelCount = data.channelCount;
            var samples = data.samples;

            var packet = new Packet().WithTag("audio")
                .WithPayload(new BytesWriter()
                    .WriteInt(segmentIndex)
                    .WriteInt(frequency)
                    .WriteInt(channelCount)
                    .WriteFloatArray(samples)
                    .Bytes
                );

            node.SendPacket(recipientID, packet, false);
            OnAudioSent?.Invoke(data);
        }

        public void Dispose() => node.Dispose();
    }
}
