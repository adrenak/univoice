#if UNIVOICE_TELEPATHY_NETWORK
using System;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;

using Telepathy;

using Adrenak.BRW;

namespace Adrenak.UniVoice.Networks {
    public class TelepathyNetwork : MonoBehaviour, IAudioNetwork {
        const string NEW_CLIENT_INIT = "NEW_CLIENT_INIT";
        const string CLIENT_JOINED = "CLIENT_JOINED";
        const string CLIENT_LEFT = "CLIENT_LEFT";
        const string AUDIO_SEGMENT = "AUDIO_SEGMENT";

        // HOSTING EVENTS
        public event Action OnCreatedChatroom;
        public event Action<Exception> OnChatroomCreationFailed;
        public event Action OnClosedChatroom;

        // JOINING EVENTS
        public event Action<int> OnJoinedChatroom;
        public event Action<Exception> OnChatroomJoinFailed;
        public event Action OnLeftChatroom;

        // PEER EVENTS
        public event Action<int> OnPeerJoinedChatroom;
        public event Action<int> OnPeerLeftChatroom;

        // AUDIO EVENTS
        public event Action<int, AudioSegment> OnAudioReceived;
        public event Action<int, AudioSegment> OnAudioSent;

        public int OwnID { get; private set; } = -1;

        public List<int> PeerIDs { get; private set; } = new List<int>();

        Server server;
        Client client;
        int port;

        [Obsolete("Use UniVoiceTelepathyNetwork.New() method instead of new keyword", true)]
        public TelepathyNetwork() { }

        public static TelepathyNetwork New(int port) {
            var go = new GameObject("UniVoiceTelepathyNetwork");
            var cted = go.AddComponent<TelepathyNetwork>();
            DontDestroyOnLoad(go);
            cted.port = port;
            cted.server = new Server(32 * 1024);
            cted.server.OnConnected += cted.OnConnected_Server;
            cted.server.OnDisconnected += cted.OnDisconnected_Server;
            cted.server.OnData += cted.OnData_Server;

            cted.client = new Client(32 * 1024);
            cted.client.OnData += cted.OnData_Client;
            cted.client.OnDisconnected += cted.OnDisconnected_Client;
            return cted;
        }

        void Update() {
            server?.Tick(100);
            client?.Tick(100);
        }

        void OnDestroy() {
            Dispose();
        }

        public void Dispose() {
            client.Disconnect();
            server.Stop();
        }

        void OnData_Client(ArraySegment<byte> data) {
            try {
                var packet = new BytesReader(data.Array);
                var tag = packet.ReadString();

                switch (tag) {
                    case NEW_CLIENT_INIT:
                        OwnID = packet.ReadInt();
                        OnJoinedChatroom?.Invoke(OwnID);
                        PeerIDs = packet.ReadIntArray().ToList();
                        foreach (var peer in PeerIDs)
                            OnPeerJoinedChatroom?.Invoke(peer);
                        break;
                    case CLIENT_JOINED:
                        var joinedID = packet.ReadInt();
                        if (!PeerIDs.Contains(joinedID))
                            PeerIDs.Add(joinedID);
                        OnPeerJoinedChatroom?.Invoke(joinedID);
                        break;
                    case CLIENT_LEFT:
                        var leftID = packet.ReadInt();
                        if (PeerIDs.Contains(leftID))
                            PeerIDs.Remove(leftID);
                        OnPeerLeftChatroom?.Invoke(leftID);
                        break;
                    case AUDIO_SEGMENT:
                        var sender = packet.ReadInt();
                        var recepient = packet.ReadInt();
                        if (recepient == OwnID) {
                            var segment = Utils.Bytes.FromByteArray<AudioSegment>(packet.ReadByteArray());
                            OnAudioReceived?.Invoke(sender, segment);
                        }
                        break;
                }
            }
            catch { }
        }

        void OnDisconnected_Client() {
            if (OwnID == -1) {
                OnChatroomJoinFailed?.Invoke(new Exception("Could not join chatroom"));
                return;
            }
            PeerIDs.Clear();
            OwnID = -1;
            OnLeftChatroom?.Invoke();
            Debug.Log("Client Disconnected");
        }

        void OnConnected_Server(int id) {
            if (id != 0) {
                if (!PeerIDs.Contains(id)) {
                    PeerIDs.Add(id);
                    Debug.Log("A new client with ID " + id + " has joined");
                }
                foreach (var peer in PeerIDs) {
                    // Let the new client know its ID
                    if (peer == id) {
                        var peersForNewClient = PeerIDs
                            .Where(x => x != peer)
                            .ToList();

                        // Server is ID 0, we add outselves to the peer list
                        // for the newly joined client
                        peersForNewClient.Add(0);

                        string peerListString = string.Join(", ", peersForNewClient);

                        var newClientPacket = new BytesWriter()
                            .WriteString(NEW_CLIENT_INIT)
                            .WriteInt(id)
                            .WriteIntArray(peersForNewClient.ToArray());
                        Debug.Log("Initializing new client with peer list: " + peerListString);
                        server.Send(peer, new ArraySegment<byte>(newClientPacket.Bytes));
                    }
                    // Let other clients know a new peer has joined
                    else {
                        var oldClientsPacket = new BytesWriter()
                            .WriteString(CLIENT_JOINED)
                            .WriteInt(id);
                        server.Send(peer, new ArraySegment<byte>(oldClientsPacket.Bytes));
                    }
                }
                OnPeerJoinedChatroom?.Invoke(id);
            }
        }

        void OnData_Server(int sender, ArraySegment<byte> data) {
            var packet = new BytesReader(data.Array);
            var tag = packet.ReadString();

            if (tag.Equals(AUDIO_SEGMENT)) {
                var audioSender = packet.ReadInt();
                var recipient = packet.ReadInt();
                var segmentBytes = packet.ReadByteArray();

                // If the audio is for the server, we invoke the audio received event.
                if (recipient == OwnID) {
                    var segment = Utils.Bytes.FromByteArray<AudioSegment>(segmentBytes);
                    OnAudioReceived?.Invoke(audioSender, segment);
                }
                // If the message is meant for someone else,
                // we forward it to the intended recipient.
                else if (PeerIDs.Contains(recipient))
                    server.Send(recipient, data);
            }
        }

        void OnDisconnected_Server(int id) {
            if (id != 0) {
                if (PeerIDs.Contains(id))
                    PeerIDs.Remove(id);
                foreach (var peer in PeerIDs) {
                    // Notify all remaining peers that someone has left 
                    var packet = new BytesWriter()
                        .WriteString(CLIENT_LEFT)
                        .WriteInt(id);
                    server.Send(peer, new ArraySegment<byte>(packet.Bytes));
                }
                OnPeerLeftChatroom?.Invoke(id);
            }
        }

        public void HostChatroom(object data = null) {
            if (!server.Active) {
                if (server.Start(port)) {
                    OwnID = 0;
                    PeerIDs.Clear();
                    OnCreatedChatroom?.Invoke();
                }
            }
            else
                Debug.LogWarning("HostChatroom failed. Already hosting a chatroom. Close and host again.");
        }

        /// <summary>
        /// Closes the chatroom, if hosting
        /// </summary>
        /// <param name="data"></param>
        public void CloseChatroom(object data = null) {
            if (server != null && server.Active) {
                server.Stop();
                PeerIDs.Clear();
                if (OwnID == -1) {
                    OnChatroomCreationFailed?.Invoke(new Exception("Could not create chatroom"));
                    return;
                }
                OwnID = -1;
                OnClosedChatroom?.Invoke();
            }
            else
                Debug.LogWarning("CloseChatroom failed. Not hosting a chatroom currently");
        }

        /// <summary>
        /// Joins a chatroom. If passed null, will connect to "localhost"
        /// </summary>
        /// <param name="data"></param>
        public void JoinChatroom(object data = null) {
            if (client.Connected || client.Connecting)
                client.Disconnect();
            if (client.Connected) {
                Debug.LogWarning("JoinChatroom failed. Already connected to a chatroom. Leave and join again.");
                return;
            }
            if (client.Connecting) {
                Debug.LogWarning("JoinChatroom failed. Currently attempting to connect to a chatroom. Leave and join again");
                return;
            }
            var ip = "localhost";
            if (data != null)
                ip = (string)data;
            client.Connect(ip, port);
        }

        /// <summary>
        /// Leave a chatroom. Data passed is not used
        /// </summary>
        /// <param name="data"></param>
        public void LeaveChatroom(object data = null) {
            if (client.Connected || client.Connecting)
                client.Disconnect();
            else
                Debug.LogWarning("LeaveChatroom failed. Currently not connected to any chatroom");
        }

        /// <summary>
        /// Send a <see cref="ChatroomAudioSegment"/> to a peer
        /// This method is used internally by <see cref="ChatroomAgent"/>
        /// invoke it manually at your own risk!
        /// </summary>
        /// <param name="peerID"></param>
        /// <param name="data"></param>
        public void SendAudioSegment(int peerID, AudioSegment data) {
            if (!server.Active && !client.Connected) return;

            var packet = new BytesWriter()
                .WriteString(AUDIO_SEGMENT)
                .WriteInt(OwnID) // Sender
                .WriteInt(peerID) // Recipient
                .WriteByteArray(Utils.Bytes.ToByteArray(data));

            if (server.Active)
                server.Send(peerID, new ArraySegment<byte>(packet.Bytes));
            else if (client.Connected)
                client.Send(new ArraySegment<byte>(packet.Bytes));

            OnAudioSent?.Invoke(peerID, data);
        }
    }
}
#endif