using System;
using System.Collections;
using System.Collections.Generic;

using Adrenak.AirPeer;

namespace Adrenak.UniVoice {
    public class AirPeerChatroomNetwork : IChatroomNetwork {
        public event Action OnChatroomCreated;
        public event Action<Exception> OnChatroomCreationFailed;
        public event Action OnChatroomClosed;

        public event Action<short> OnJoined;
        public event Action OnLeft;
        
        public event Action<short> OnPeerJoined;
        public event Action<short> OnPeerLeft;
        
        public event Action<short, int, int, int, float[]> OnAudioReceived;
        public event Action<short, int, int, int, float[]> OnAudioSent;

        public short ID => node.ID;

        public List<short> Peers => ID != -1 ? node.Peers : new List<short>();

        public string CurrentRoomName => ID != -1 ? node.Address : null;

        APNode node;

        public AirPeerChatroomNetwork(string signallingServerURL) {
            node = new APNode(signallingServerURL);

            node.OnServerStartSuccess += () => OnChatroomCreated?.Invoke();
            node.OnServerStartFailure += ex => OnChatroomCreationFailed?.Invoke(ex);
            node.OnServerStop += () => OnChatroomClosed?.Invoke();

            node.OnReceiveID += id => OnJoined?.Invoke(id);
            node.OnDisconnected += () => OnLeft?.Invoke();

            node.OnClientJoined += id => OnPeerJoined?.Invoke(id);
            node.OnClientLeft += id => OnPeerLeft?.Invoke(id);

            node.OnPacketReceived += (sender, packet) => {

            };
        }

        public void CreateChatroom(string chatroomName) =>
            node.StartServer(chatroomName);

        public void CloseChatroom() =>
            node.StopServer();

        public void JoinChatroom(string chatroomName) =>
            node.Connect(chatroomName);

        public void LeaveChatroom() =>
            node.Disconnect();

        public void SendAudioSegment(short recipientID, int segmentIndex, int frequency, int channelCount, float[] samples) {

        }

        public void Dispose() =>
            node.Dispose();
    }
}
