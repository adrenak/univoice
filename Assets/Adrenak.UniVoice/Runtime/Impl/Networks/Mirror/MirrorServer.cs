// Notes:
// In Mirror 89.11.0, the OnServerConnectedWithAddress event was added
// https://github.com/MirrorNetworking/Mirror/releases/tag/v89.11.0
// OnServerConnected no longer seems to work?

#if MIRROR
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

            mirrorEvents = MirrorModeObserver.New("for MirrorServer");
            mirrorEvents.ModeChanged += OnModeChanged;

            NetworkServer.RegisterHandler<MirrorMessage>(OnReceivedMessage, false);
        }

        public void Dispose() {
            mirrorEvents.ModeChanged -= OnModeChanged;
            NetworkServer.UnregisterHandler<MirrorMessage>();
            OnServerShutdown();
        }

        void OnServerStarted() {
#if MIRROR_89_OR_NEWER
            NetworkManager.singleton.transport.OnServerConnectedWithAddress += OnServerConnected;
#else
            NetworkManager.singleton.transport.OnServerConnected += OnServerConnected;
#endif
            NetworkManager.singleton.transport.OnServerDisconnected += OnServerDisconnected;
            OnServerStart?.Invoke();
        }

        void OnServerShutdown() {
#if MIRROR_89_OR_NEWER
            NetworkManager.singleton.transport.OnServerConnectedWithAddress -= OnServerConnected;
#else
            NetworkManager.singleton.transport.OnServerConnected -= OnServerConnected;
#endif            
            NetworkManager.singleton.transport.OnServerDisconnected -= OnServerDisconnected;
            ClientIDs.Clear();
            ClientVoiceSettings.Clear();
            OnServerStop?.Invoke();
        }

        void OnModeChanged(NetworkManagerMode oldMode, NetworkManagerMode newMode) {
            // For some reason, handlers don't always work as expected when the connection mode changes
            NetworkServer.ReplaceHandler<MirrorMessage>(OnReceivedMessage, false);

            // If in Host mode, the server and internal client have both started and the client connects immediately.
            // The host client seems to have ID 0 always, so we trigger a new client connection using id 0.
            if (newMode == NetworkManagerMode.Host) {
                OnServerStarted();
                OnServerConnected(0, "localhost");
            }
            else if (newMode == NetworkManagerMode.ServerOnly) {
                // If a Host changes to ServerOnly, we disconnect the internal client
                if (oldMode == NetworkManagerMode.Host)
                    OnServerDisconnected(0);
                // But if this machine is going from Offline to ServerOnly, only the server is starting
                else if (oldMode == NetworkManagerMode.Offline)
                    OnServerStarted();
            }
            // If a Host or ServerOnly goes offline 
            else if (newMode == NetworkManagerMode.Offline && (oldMode == NetworkManagerMode.ServerOnly || oldMode == NetworkManagerMode.Host)) {
                // We check if it was a Host before and disconnect the internal client
                if (oldMode == NetworkManagerMode.Host)
                    OnServerDisconnected(0);
                OnServerShutdown();
            }
        }

        void OnReceivedMessage(NetworkConnectionToClient connection, MirrorMessage message) {
            var clientId = connection.connectionId;
            var reader = new BytesReader(message.data);
            var tag = reader.ReadString();

            // Server forwards the received audio from a client to other clients based on voice settings.
            // Client can mute or deafen each other using IDs as well as tags
            if (tag.Equals(MirrorMessageTags.AUDIO_FRAME)) {
                // We start with all the peers except the one that's
                // sent the audio 
                var peersToForwardAudioTo = ClientIDs
                    .Where(x => x != clientId);

                // Check the voice settings of the sender and eliminate any peers the sender
                // may have deafened
                if (ClientVoiceSettings.TryGetValue(clientId, out var senderSettings)) {
                    // If the client sending the audio has deafened everyone,
                    // we simply return. Sender's audio should not be forwarded to anyone.
                    if (senderSettings.deafenAll)
                        return;

                    // Filter the recipient list by removing all peers that the sender has
                    // deafened using ID
                    peersToForwardAudioTo = peersToForwardAudioTo
                        .Where(x => !senderSettings.deafenedPeers.Contains(x));

                    // Further filter the recipient list by removing peers that the sender has
                    // deafened using tags
                    peersToForwardAudioTo = peersToForwardAudioTo.Where(peer => {
                        // Get the voice settings of the peer
                        if (ClientVoiceSettings.TryGetValue(peer, out VoiceSettings peerVoiceSettings)) {
                            // Check if sender has not deafened peer using tag
                            var hasDeafenedPeer = senderSettings.deafenedTags.Intersect(peerVoiceSettings.myTags).Count() > 0;
                            return !hasDeafenedPeer;
                        }
                        // If peer doesn't have voice settings, we can keep the peer in the list
                        else {
                            return true;
                        }
                    });
                }

                // We iterate through each recipient peer that the sender wants to send audio to, checking if
                // they have muted the sender, before forwarding the audio to them.
                foreach (var recipient in peersToForwardAudioTo) {
                    // Get the settings of a potential recipient
                    if (ClientVoiceSettings.TryGetValue(recipient, out var recipientSettings)) {
                        // If a peer has muted everyone, don't send audio
                        if (recipientSettings.muteAll)
                            continue;

                        // If the peers has muted the sender using ID, skip sending audio
                        if (recipientSettings.mutedPeers.Contains(clientId))
                            continue;

                        // If the peer has muted the sender using tag, skip sending audio
                        if (recipientSettings.mutedTags.Intersect(senderSettings.myTags).Count() > 0)
                            continue;
                    }
                    SendToClient(recipient, message.data, Channels.Unreliable);
                }
            }
            else if (tag.Equals(MirrorMessageTags.VOICE_SETTINGS)) {
                // We create the VoiceSettings object by reading from the reader
                // and update the peer voice settings map
                var muteAll = reader.ReadInt() == 1 ? true : false;
                var mutedPeers = reader.ReadIntArray().ToList();
                var deafenAll = reader.ReadInt() == 1 ? true : false;
                var deafenedPeers = reader.ReadIntArray().ToList();

                var myTagsVal = reader.ReadString();
                var myTags = myTagsVal.Equals(",") ? new List<string>() : myTagsVal.Split(",").ToList();
                
                var mutedTagsVal = reader.ReadString();
                var mutedTags = mutedTagsVal.Equals(",") ? new List<string>() : mutedTagsVal.Split(",").ToList();
                
                var deafenedTagsVal = reader.ReadString();
                var deafenedTags = deafenedTagsVal.Equals(",") ? new List<string>() : deafenedTagsVal.Split(",").ToList();
                
                var voiceSettings = new VoiceSettings {
                    muteAll = muteAll,
                    mutedPeers = mutedPeers,
                    deafenAll = deafenAll,
                    deafenedPeers = deafenedPeers,
                    myTags = myTags,
                    mutedTags = mutedTags,
                    deafenedTags = deafenedTags
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
            // Not sure if this needs to be done, but being extra cautious here
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

                    string log = $"Initializing new client with ID {connId}";
                    if (otherPeerIDs.Length > 0)
                        log += $" and peer list {string.Join(", ", otherPeerIDs)}";
                    Debug.unityLogger.Log(LogType.Log, TAG, log);
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
            // Not sure if this needs to be done, but being extra cautious here
            NetworkServer.ReplaceHandler<MirrorMessage>(OnReceivedMessage, false);

            ClientIDs.Remove(connId);
            Debug.unityLogger.Log(LogType.Log, TAG, $"Client {connId} disconnected");

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
            foreach (var conn in NetworkServer.connections)
                if (conn.Key == connId)
                    return conn.Value;
            return null;
        }
    }
}
#endif