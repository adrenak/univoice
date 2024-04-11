#if UNIVOICE_MIRROR_NETWORK
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using UnityEngine;

using Mirror;

using Adrenak.BRW;

namespace Adrenak.UniVoice.Networks {
    public class MirrorNetwork : IAudioNetwork {
        // Packet tags
        const string NEW_CLIENT_INIT = "NEW_CLIENT_INIT";
        const string CLIENT_JOINED = "CLIENT_JOINED";
        const string CLIENT_LEFT = "CLIENT_LEFT";
        const string AUDIO_FRAME = "AUDIO_FRAME";

        // Chatroom Creation events
        public event Action OnCreatedChatroom;
        public event Action<Exception> OnChatroomCreationFailed;
        public event Action OnClosedChatroom;

        // Chatroom Joining events
        public event Action<int> OnJoinedChatroom;
        public event Action<Exception> OnChatroomJoinFailed;
        public event Action OnLeftChatroom;

        // Peer events
        public event Action<int> OnPeerJoinedChatroom;
        public event Action<int> OnPeerLeftChatroom;

        // Audio events
        public event Action<int, AudioFrame> OnAudioReceived;
        public event Action<int, AudioFrame> OnAudioSent;

        // Peer ID management
        public int OwnID { get; private set; } = -1;
        public List<int> PeerIDs { get; private set; } = new List<int>();

        // UniVoice peer ID <-> Mirror connection ID mapping
        int peerCount = 0;
        readonly Dictionary<int, int> clientMap = new Dictionary<int, int>();

        readonly UpdateHook updateHook;

        public MirrorNetwork() {
            // Unity update hook
            updateHook = UpdateHook.Create();
            updateHook.OnUpdate += OnUpdate;

            NetworkServer.RegisterHandler<UniVoiceMessage>(OnServerMessage, false);
            NetworkClient.RegisterHandler<UniVoiceMessage>(OnClientMessage, false);

            // Called on a client joining and leaving a server
            NetworkManager.singleton.transport.OnClientConnected += OnClientConnected;
            NetworkManager.singleton.transport.OnClientDisconnected += OnClientDisconnected;

            // When a server when a client joins and leaves 
            NetworkManager.singleton.transport.OnServerConnected += OnServerConnected;
            NetworkManager.singleton.transport.OnServerDisconnected += OnServerDisconnected;
        }

        // Per frame code to detect change in NetworkManager mode
        NetworkManagerMode lastMode = NetworkManagerMode.Offline;
        void OnUpdate() {
            // NetworkManager calls Shutdown on NetworkServer and NetworkClient when they 
            // stop or disconnect. This causes the handlers to be cleared.
            // I can't figure out when to re-register the handler as there doesn't seem to be an
            // event for these. So just keep on replacing the handler every frame.
            // It is an ugly fix, but for now this works.
            NetworkServer.ReplaceHandler<UniVoiceMessage>(OnServerMessage, false);
            NetworkClient.ReplaceHandler<UniVoiceMessage>(OnClientMessage, false);

            var newMode = NetworkManager.singleton.mode;
            if(lastMode != newMode) {
                OnModeChanged(lastMode, newMode);
                lastMode = newMode;
            }
        }

        void OnModeChanged(NetworkManagerMode oldMode, NetworkManagerMode newMode) {
            // If we go to offline
            if(newMode == NetworkManagerMode.Offline) {
                OwnID = -1;
                PeerIDs.Clear();
                clientMap.Clear();
                if (oldMode == NetworkManagerMode.ServerOnly || oldMode == NetworkManagerMode.Host) {
                    OnClosedChatroom?.Invoke();
                }
            }
        }

        public void SendAudioFrame(int recipientPeerId, AudioFrame data) {
            if (IsOffline) return;
        
            // We write a packet with this data:
            // tag: string
            // senderPeerID: int
            // recipientPeerID: int
            // audio data: byte[]
            var packet = new BytesWriter()
                .WriteString(AUDIO_FRAME)
                .WriteInt(OwnID)
                .WriteInt(recipientPeerId)
                .WriteByteArray(Utils.Bytes.ToByteArray(data));

            if (IsServerOrHost) 
                SendToClient(recipientPeerId, packet.Bytes);
            else if(IsClient)
                SendToServer(packet.Bytes);

            OnAudioSent?.Invoke(recipientPeerId, data);
        }

        async void SendToClient(int peerID, byte[] bytes, int delay = 0) {
            if (IsServerOrHost) {
                if (delay != 0)
                    await Task.Delay(delay);

                foreach(var conn in NetworkServer.connections) {
                    if (conn.Key == GetConnectionIdFromPeerId(peerID))
                        conn.Value.Send(new UniVoiceMessage {
                            sender = OwnID,
                            recipient = peerID,
                            data = bytes
                        }, Channels.Unreliable);
                }
            }
        }

        void SendToServer(byte[] bytes) {
            if (NetworkManager.singleton.mode == NetworkManagerMode.ClientOnly)
                NetworkClient.Send(new UniVoiceMessage { 
                    sender = OwnID,
                    recipient = -1,
                    data = bytes
                }, Channels.Unreliable);
        }

        void OnClientConnected() {
            Debug.Log("Client connected to server. Awaiting initialization from server. " +
            "Connection ID : " + NetworkClient.connection.connectionId);
        }

        void OnClientDisconnected() {
            // If the client disconnects while own ID is -1, that means
            // it haven't connected earlier and the connection attempt has failed.
            if (!IsServerOrHost && OwnID == -1) {
                OnChatroomJoinFailed?.Invoke(new Exception("Could not join chatroom"));
                return;
            }

            // This method is *also* called on the server when the server is shutdown.
            // So we check peer ID to ensure that we're running this only on a peer.
            if (OwnID > 0) {
                OwnID = -1;
                PeerIDs.Clear();
                clientMap.Clear();
                OnLeftChatroom?.Invoke();
            }
        }

        void OnServerConnected(int connId) {
            // TODO: This causes the chatroom is to detected as created only when
            // the first peer joins. While this doesn't cause any bugs, it isn't right.
            if (IsServerOrHost && OwnID != 0) {
                OwnID = 0;
                OnCreatedChatroom?.Invoke();
            }

            // We get a peer ID for this connection id
            var peerId = RegisterConnectionId(connId);

            // We go through each the peer that the server has registered
            foreach (var peer in PeerIDs) {
                // To the new peer, we send data to initialize it with.
                // This includes the following:
                // - peer Id: int: This tells the new peer its ID in the chatroom
                // - existing peers: int[]: This tells the new peer the IDs of the
                // peers that are already in the chatroom
                if (peer == peerId) {
                    // Get all the existing peer IDs except that of the newly joined peer
                    var existingPeersInitPacket = PeerIDs
                        .Where(x => x != peer)
                        .ToList();

                    // Server is ID 0, we add outselves to the peer list
                    // for the newly joined client
                    existingPeersInitPacket.Add(0);

                    var newClientPacket = new BytesWriter()
                        .WriteString(NEW_CLIENT_INIT)
                        .WriteInt(peerId)
                        .WriteIntArray(existingPeersInitPacket.ToArray());

                    // Server_OnClientConnected gets invoked as soon as a client connects
                    // to the server. But we use NetworkServer.SendToAll to send our packets
                    // and it seems the new Mirror Connection ID is not added to the KcpTransport
                    // immediately, so we send this with an artificial delay of 100ms.
                    SendToClient(-1, newClientPacket.Bytes, 100);

                    string peerListString = string.Join(", ", existingPeersInitPacket);
                    Debug.Log($"Initializing new client with ID {peerId} and peer list {peerListString}");
                }
                // To the already existing peers, we let them know a new peer has joined
                else {
                    var newPeerNotifyPacked = new BytesWriter()
                        .WriteString(CLIENT_JOINED)
                        .WriteInt(peerId);
                    SendToClient(peer, newPeerNotifyPacked.Bytes);
                }
            }
            OnPeerJoinedChatroom?.Invoke(peerId);
        }

        void OnServerDisconnected(int connId) {
            // We use the peer map to get the peer ID for this connection ID
            var leftPeerId = GetPeerIdFromConnectionId(connId);

            // We now go ahead with the server handling a client leaving
            // Remove the peer from our peer list
            if (PeerIDs.Contains(leftPeerId))
                PeerIDs.Remove(leftPeerId);

            // Remove the peer-connection ID pair from the map
            if (clientMap.ContainsKey(leftPeerId))
                clientMap.Remove(leftPeerId);

            // Notify all remaining peers that a peer has left 
            // so they can update their peer lists
            foreach (var peerId in PeerIDs) {
                var packet = new BytesWriter()
                    .WriteString(CLIENT_LEFT)
                    .WriteInt(leftPeerId);
                SendToClient(peerId, packet.Bytes);
            }
            OnPeerLeftChatroom?.Invoke(leftPeerId);
        }

        void OnClientMessage(UniVoiceMessage message) {
            // The server can have a connection to itself, so we only process messages
            // on an instance that is the client.
            if (NetworkManager.singleton.mode != NetworkManagerMode.ClientOnly) return;
            if (OwnID == 0) return;

            // Unless we're the recipient of the message or the message is a broadcast
            // (recipient == -1), we don't process the message ahead.
            if (message.recipient == -1 && message.recipient != OwnID) return;

            try {
                var bytes = message.data;
                var packet = new BytesReader(bytes);
                var tag = packet.ReadString();

                switch (tag) {
                    // New client initialization has the following data (in this order):
                    // The peers ID: int
                    // The existing peers in the chatroom: int[]
                    case NEW_CLIENT_INIT:
                        // Get self ID and fire that joined chatroom event
                        OwnID = packet.ReadInt();
                        OnJoinedChatroom?.Invoke(OwnID);

                        // Get the existing peer IDs from the message and fire
                        // the peer joined event for each of them
                        PeerIDs = packet.ReadIntArray().ToList();
                        PeerIDs.ForEach(x => OnPeerJoinedChatroom?.Invoke(x));

                        Debug.Log($"Initialized self with ID {OwnID} and peers {string.Join(", ", PeerIDs)}");
                        break;

                    // When a new peer joins, the existing peers add it to their state
                    // and fire the peer joined event
                    case CLIENT_JOINED:
                        var joinedID = packet.ReadInt();
                        if (!PeerIDs.Contains(joinedID))
                            PeerIDs.Add(joinedID);
                        OnPeerJoinedChatroom?.Invoke(joinedID);
                        break;

                    // When a peer leaves, the existing peers remove it from their state
                    // and fire the peer left event
                    case CLIENT_LEFT:
                        var leftID = packet.ReadInt();
                        if (PeerIDs.Contains(leftID))
                            PeerIDs.Remove(leftID);
                        OnPeerLeftChatroom?.Invoke(leftID);
                        break;

                    // When this peer receives audio, we find out the we we're the intended
                    // recipient of that audio segment. If so, we fire the audio received event.
                    // The data is as follows:
                    // sender: int
                    // recipient: int
                    // audio: byte[]
                    case AUDIO_FRAME:
                        var sender = packet.ReadInt();
                        var recepient = packet.ReadInt();
                        if (recepient == OwnID) {
                            var segment = Utils.Bytes.FromByteArray<AudioFrame>(packet.ReadByteArray());
                            OnAudioReceived?.Invoke(sender, segment);
                        }
                        break;
                }
            }
            catch (Exception e) {
                Debug.LogError(e);
            }
        }

        void OnServerMessage(NetworkConnectionToClient connection, UniVoiceMessage message) {
            if (!IsServerOrHost) return;

            var bytes = message.data;
            var packet = new BytesReader(bytes);
            var tag = packet.ReadString();

            if (tag.Equals(AUDIO_FRAME)) {
                var audioSender = packet.ReadInt();
                var recipient = packet.ReadInt();
                var segmentBytes = packet.ReadByteArray();

                // If the audio is for the server, we invoke the audio received event.
                if (recipient == OwnID) {
                    var segment = Utils.Bytes.FromByteArray<AudioFrame>(segmentBytes);
                    OnAudioReceived?.Invoke(audioSender, segment);
                }
                // If the message is meant for someone else,
                // we forward it to the intended recipient.
                else if (PeerIDs.Contains(recipient))
                    SendToClient(recipient, bytes);
            }
        }

        /// <summary>
        /// Returns the UniVoice peer Id corresponding to a previously
        /// registered Mirror connection Id
        /// </summary>
        /// <param name="connId">The connection Id to lookup</param>
        /// <returns>THe UniVoice Peer ID</returns>
        int GetPeerIdFromConnectionId(int connId) {
            foreach (var pair in clientMap) {
                if (pair.Value == connId)
                    return pair.Key;
            }
            return -1;
        }

        int GetConnectionIdFromPeerId(int peerId) {
            foreach (var pair in clientMap) {
                if (pair.Key == peerId)
                    return pair.Key;
            }
            return -1;
        }

        /// <summary>
        /// In Mirror, Connection ID can be a very large number, 
        /// for exmaple KcpTransport connection Ids can be something like 390231886
        /// Since UniVoice uses sequential int values to store peers, we generate a peer ID
        /// from any int connection Id and use a dictionary to store them in pairs.
        /// </summary>
        /// <param name="connId">The Mirror connection ID to be registered</param>
        /// <returns>The UniVoice Peer ID after registration</returns>
        int RegisterConnectionId(int connId) {
            peerCount++;
            clientMap.Add(peerCount, connId);
            PeerIDs.Add(peerCount);
            return peerCount;
        }    

        bool IsServerOrHost {
            get {
                var mode = NetworkManager.singleton.mode;
                return mode == NetworkManagerMode.Host
                    || mode == NetworkManagerMode.ServerOnly;
            }
        }

        bool IsClient =>
            NetworkManager.singleton.mode == NetworkManagerMode.ClientOnly;

        bool IsOffline =>
            NetworkManager.singleton.mode == NetworkManagerMode.Offline;

        [Serializable]
        public struct UniVoiceMessage : NetworkMessage {
            [Obsolete]
            public int sender;
            [Obsolete]
            public int recipient;
            public byte[] data;
        }

        public void Dispose() { }
    }
}
#endif