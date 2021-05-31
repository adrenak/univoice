using System;
using System.Collections.Generic;

using Adrenak.AirPeer;

namespace Adrenak.UniVoice {
    /// <summary>
    /// A <see cref="IChatroomNetwork"/> implementation using AirPeer
    /// For more on AirPeer, visit https://www.vatsalambastha.com/airpeer
    /// </summary>
    public class AirPeerChatroomNetwork : IChatroomNetwork {
        public event Action OnChatroomCreated;
        public event Action<Exception> OnChatroomCreationFailed;
        public event Action OnChatroomClosed;

        public event Action<short> OnJoined;
        public event Action<Exception> OnJoiningFailed;
        public event Action OnLeft;
        
        public event Action<short> OnPeerJoined;
        public event Action<short> OnPeerLeft;
        
        public event Action<ChatroomAudioDTO> OnAudioReceived;
        public event Action<ChatroomAudioDTO> OnAudioSent;

        public short OwnID => node.ID;

        public List<short> PeerIDs => OwnID != -1 ? node.Peers : new List<short>();

        public string CurrentChatroomName => OwnID != -1 ? node.Address : null;

        APNode node;

        public AirPeerChatroomNetwork(string signallingServerURL) {
            node = new APNode(signallingServerURL);

            node.OnServerStartSuccess += () => OnChatroomCreated?.Invoke();
            node.OnServerStartFailure += ex => OnChatroomCreationFailed?.Invoke(ex);
            node.OnServerStop += () => OnChatroomClosed?.Invoke();

            node.OnConnectionFailed += ex => OnJoiningFailed?.Invoke(ex);
            node.OnReceiveID += id => OnJoined?.Invoke(id);
            node.OnDisconnected += () => OnLeft?.Invoke();

            node.OnClientJoined += id => OnPeerJoined?.Invoke(id);
            node.OnClientLeft += id => OnPeerLeft?.Invoke(id);

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

        public void HostChatroom(string chatroomName) => node.StartServer(chatroomName);

        public void CloseChatroom() => node.StopServer();

        public void JoinChatroom(string chatroomName) => node.Connect(chatroomName);

        public void LeaveChatroom() => node.Disconnect();

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
