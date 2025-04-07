// Notes:
// In Mirror 89.11.0, the OnServerConnectedWithAddress event was added
// https://github.com/MirrorNetworking/Mirror/releases/tag/v89.11.0
// OnServerConnected no longer seems to work?

#if UNIVOICE_MIRROR_NETWORK || UNIVOICE_NETWORK_MIRROR
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using UnityEngine;

using Mirror;
using Adrenak.BRW;

namespace Adrenak.UniVoice.Networks {
    /// <summary>
    /// Activate this class by including the UNIVOICE_MIRROR_NETWORK compilaton symbol
    /// in your project.
    /// This is an implementation of the <see cref="IAudioServer{T}"/> interface for Mirror.
    /// It uses the Mirror transport to send and receive UniVoice audio data to and from clients.
    /// </summary>
    public class MirrorServer : IAudioServer<int> {
        const string TAG = "[MirrorServer]";

        public event Action OnServerStart;

        public event Action OnServerStop;

        public event Action OnClientVoiceSettingsUpdated;

        public List<int> ClientIDs { get; private set; }

        public Dictionary<int, VoiceSettings> ClientVoiceSettings { get; private set; }

        readonly MirrorModeObserver mirrorEvents;

        public MirrorServer() {
            ClientIDs = new List<int>();
            ClientVoiceSettings = new Dictionary<int, VoiceSettings>();

            mirrorEvents = MirrorModeObserver.New();
            mirrorEvents.ModeChanged += OnModeChanged;

            NetworkServer.RegisterHandler<MirrorMessage>(OnReceivedMessage, false);

#if MIRROR_89_OR_NEWER
            NetworkManager.singleton.transport.OnServerConnectedWithAddress += OnServerConnected;
#else
            NetworkManager.singleton.transport.OnServerConnected += OnServerConnected;
#endif
            NetworkManager.singleton.transport.OnServerDisconnected += OnServerDisconnected;
        }
        
        void IDisposable.Dispose() {
            mirrorEvents.ModeChanged -= OnModeChanged;

            NetworkServer.UnregisterHandler<MirrorMessage>();

#if MIRROR_89_OR_NEWER
            NetworkManager.singleton.transport.OnServerConnectedWithAddress -= OnServerConnected;
#else
            NetworkManager.singleton.transport.OnServerConnected -= OnServerConnected;
#endif
            NetworkManager.singleton.transport.OnServerDisconnected -= OnServerDisconnected;
        }

        void OnModeChanged(NetworkManagerMode oldMode, NetworkManagerMode newMode) {
            NetworkServer.ReplaceHandler<MirrorMessage>(OnReceivedMessage, false);

            if((newMode == NetworkManagerMode.ServerOnly || newMode == NetworkManagerMode.Host)
            && (oldMode != NetworkManagerMode.ServerOnly && oldMode != NetworkManagerMode.Host)) {
                OnServerStart?.Invoke();
            }
            else if(newMode == NetworkManagerMode.Offline) {
                ClientIDs.Clear();
                ClientVoiceSettings.Clear();
                OnServerStop?.Invoke();
            }
        }

        void OnReceivedMessage(NetworkConnectionToClient connection, MirrorMessage message) {
            var clientId = connection.connectionId;
            var reader = new BytesReader(message.data);
            var tag = reader.ReadString();

            if (tag.Equals(MirrorMessageTags.AUDIO_FRAME)) {
                // We start with all the peers except the one that's
                // sent the audio 
                var peersToForwardAudioTo = ClientIDs
                    .Where(x => x != clientId);

                // Consider the voice settings of the sender to see who
                // the sender doesn't want to send audio to
                if (ClientVoiceSettings.TryGetValue(clientId, out var senderSettings)) {
                    // If the client sending the audio has deafened everyone
                    // to their audio, we simply return
                    if (senderSettings.deafenAll)
                        return;

                    // Else, we remove all the peers that the sender has
                    // deafened themselves to
                    peersToForwardAudioTo = peersToForwardAudioTo
                        .Where(x => !senderSettings.deafenedPeers.Contains(x));
                }

                // We iterate through each recipient peer that the sender wants to send
                // audio to, checking if they have muted the sender in which case
                // we skip that recipient
                foreach (var receiver in peersToForwardAudioTo) {
                    if (ClientVoiceSettings.TryGetValue(receiver, out var receiverSettings)) {
                        if (receiverSettings.muteAll)
                            continue;
                        if (receiverSettings.mutedPeers.Contains(clientId))
                            continue;
                    }
                    SendToClient(receiver, message.data, Channels.Unreliable);
                }
            }
            else if (tag.Equals(MirrorMessageTags.VOICE_SETTINGS)) {
                //Debug.unityLogger.Log(LogType.Log, TAG, "Mirror server stopped");
                // We create the VoiceSettings object by reading from the reader
                // and update the peer voice settings map
                var muteAll = reader.ReadInt() == 1 ? true : false;
                var mutedPeers = reader.ReadIntArray().ToList();
                var deafenAll = reader.ReadInt() == 1 ? true : false;
                var deafenedPeers = reader.ReadIntArray().ToList();
                var voiceSettings = new VoiceSettings {
                    muteAll = muteAll,
                    mutedPeers = mutedPeers,
                    deafenAll = deafenAll,
                    deafenedPeers = deafenedPeers
                };
                if (ClientVoiceSettings.ContainsKey(clientId))
                    ClientVoiceSettings[clientId] = voiceSettings;
                else
                    ClientVoiceSettings.Add(clientId, voiceSettings);
                OnClientVoiceSettingsUpdated?.Invoke();
            }
        }

        // When a new Mirror client connects
#if MIRROR_89_OR_NEWER
        void OnServerConnected(int connId, string addr) {
#else
        void OnServerConnected(int connId) {
#endif
            NetworkServer.ReplaceHandler<MirrorMessage>(OnReceivedMessage, false);

            Debug.unityLogger.Log(LogType.Log, TAG, $"Client {connId} connected");
            ClientIDs.Add(connId);

            foreach (var peer in ClientIDs) {
                // To the new peer, we send data to initialize it with.
                // This includes the following:
                // - its own ID (int) This tells the new peer its ID in the chatroom
                // - IDs of other peers (int[]) This tells the new peer the IDs of the
                // peers that are already in the chatroom
                if (peer == connId) {
                    // Get all the existing peer IDs except that of the newly joined peer
                    var otherPeerIDs = ClientIDs
                        .Where(x => x != connId)
                        .ToArray();

                    var newClientPacket = new BytesWriter()
                        .WriteString(MirrorMessageTags.PEER_INIT)
                        .WriteInt(connId)
                        .WriteIntArray(otherPeerIDs);

                    // We initialize the new client/peer with some delay. A delay here MAY not be
                    // required but I faced some issues with immediate initialization earlier.
                    SendToClientDelayed(connId, newClientPacket.Bytes, Channels.Reliable, 100);

                    string peerListString = string.Join(", ", otherPeerIDs);
                    Debug.unityLogger.Log(LogType.Log, TAG, 
                        $"Initializing new client with ID {connId} and peer list {peerListString}");
                }
                // To the already existing peers, we let them know a new peer has joined
                // by sending the new peer ID to them.
                else {
                    var newPeerNotifyPacked = new BytesWriter()
                        .WriteString(MirrorMessageTags.PEER_JOINED)
                        .WriteInt(connId);
                    Debug.unityLogger.Log(
                        LogType.Log, TAG,
                        $"Notified client {peer} about new client {connId}");
                    SendToClient(peer, newPeerNotifyPacked.Bytes, Channels.Reliable);
                }
            }
        }

        void OnServerDisconnected(int connId) {
            NetworkServer.ReplaceHandler<MirrorMessage>(OnReceivedMessage, false);

            ClientIDs.Remove(connId);
            Debug.unityLogger.Log(LogType.Log, TAG,
                $"Client {connId} disconnected");

            // Notify all remaining peers that a peer has left 
            foreach (var peerId in ClientIDs) {
                var packet = new BytesWriter()
                    .WriteString(MirrorMessageTags.PEER_LEFT)
                    .WriteInt(connId);
                Debug.unityLogger.Log(LogType.Log, TAG,
                    $"Notified client {peerId} about {connId} leaving");
                SendToClient(peerId, packet.Bytes, Channels.Reliable);
            }
        }

        async void SendToClientDelayed(int peerID, byte[] bytes, int channel, int delayMS) {
            await Task.Delay(delayMS);
            SendToClient(peerID, bytes, channel);
        }

        void SendToClient(int clientConnId, byte[] bytes, int channel) {
            var message = new MirrorMessage {
                data = bytes
            };

            var conn = GetConnectionToClient(clientConnId);
            if (conn != null)
                conn.Send(message, channel);
        }

        NetworkConnectionToClient GetConnectionToClient(int connId) {
            foreach(var conn in NetworkServer.connections) 
                if (conn.Key == connId)
                    return conn.Value;
            return null;
        }
    }
}
#endif