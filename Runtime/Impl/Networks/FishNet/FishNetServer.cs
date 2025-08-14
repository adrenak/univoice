#if FISHNET
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Adrenak.BRW;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// This is an implementation of the <see cref="IAudioServer{T}"/> interface for FishNet.
    /// It uses the FishNet to send and receive UniVoice audio data to and from clients.
    /// </summary>
    public class FishNetServer : IAudioServer<int>
    {
        private const string TAG = "[FishNetServer]";

        public event Action OnServerStart;
        public event Action OnServerStop;
        public event Action OnClientVoiceSettingsUpdated;

        public List<int> ClientIDs { get; private set; }
        public Dictionary<int, VoiceSettings> ClientVoiceSettings { get; private set; }
        
        private NetworkManager _networkManager;
        private List<int> _startedTransports = new();

        public FishNetServer()
        {
            ClientIDs = new List<int>();
            ClientVoiceSettings = new Dictionary<int, VoiceSettings>();

            _networkManager = InstanceFinder.NetworkManager;
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionStateChanged;
            _networkManager.ServerManager.OnRemoteConnectionState += OnServerRemoteConnectionStateChanged;
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionStateChanged;
            _networkManager.ServerManager.RegisterBroadcast<FishNetBroadcast>(OnReceivedMessage, false);
        }

        public void Dispose()
        {
            if (_networkManager)
            {
                _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionStateChanged;
                _networkManager.ServerManager.OnRemoteConnectionState -= OnServerRemoteConnectionStateChanged;
                _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionStateChanged;
                _networkManager.ServerManager.UnregisterBroadcast<FishNetBroadcast>(OnReceivedMessage);
            }
            OnServerShutdown();
        }

        private void OnServerStarted()
        {
            OnServerStart?.Invoke();
        }

        private void OnServerShutdown()
        {
            ClientIDs.Clear();
            ClientVoiceSettings.Clear();
            OnServerStop?.Invoke();
        }
        
        private void OnServerRemoteConnectionStateChanged(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                OnServerConnected(connection.ClientId);
            }
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                OnServerDisconnected(connection.ClientId);
            }
        }

        private void OnServerConnectionStateChanged(ServerConnectionStateArgs args)
        {
            // Connection can change for each transport, so we need to track them
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                var wasStarted = _startedTransports.Count != 0;
                _startedTransports.Add(args.TransportIndex);
                if (!wasStarted) 
                    OnServerStarted();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _startedTransports.Remove(args.TransportIndex);
                if(_startedTransports.Count == 0)
                    OnServerShutdown();
            }
        }

        private void OnClientConnectionStateChanged(ClientConnectionStateArgs args)
        {
            // TODO - do we need to check if host or is this enough?
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                OnServerConnected(0);
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                OnServerDisconnected(0);
            }
        }

        private void OnReceivedMessage(NetworkConnection connection, FishNetBroadcast message, Channel channel)
        {
            var clientId = connection.ClientId;
            var reader = new BytesReader(message.data);
            var tag = reader.ReadString();

            if (tag.Equals(FishNetBroadcastTags.AUDIO_FRAME))
            {
                // We start with all the peers except the one that's
                // sent the audio 
                var peersToForwardAudioTo = ClientIDs
                    .Where(x => x != clientId);

                // Check the voice settings of the sender and eliminate any peers the sender
                // may have deafened
                if (ClientVoiceSettings.TryGetValue(clientId, out var senderSettings))
                {
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
                    peersToForwardAudioTo = peersToForwardAudioTo.Where(peer =>
                    {
                        // Get the voice settings of the peer
                        if (ClientVoiceSettings.TryGetValue(peer, out var peerVoiceSettings))
                        {
                            // Check if sender has not deafened peer using tag
                            var hasDeafenedPeer = senderSettings.deafenedTags.Intersect(peerVoiceSettings.myTags).Any();
                            return !hasDeafenedPeer;
                        }
                        // If peer doesn't have voice settings, we can keep the peer in the list
                        return true;
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
                        if (recipientSettings.mutedTags.Intersect(senderSettings.myTags).Any())
                            continue;
                    }
                    SendToClient(recipient, message.data, Channel.Unreliable);
                }
            }
            else if (tag.Equals(FishNetBroadcastTags.VOICE_SETTINGS)) {
                //Debug.unityLogger.Log(LogType.Log, TAG, "FishNet server stopped");
                // We create the VoiceSettings object by reading from the reader
                // and update the peer voice settings map
                var muteAll = reader.ReadInt() == 1;
                var mutedPeers = reader.ReadIntArray().ToList();
                var deafenAll = reader.ReadInt() == 1;
                var deafenedPeers = reader.ReadIntArray().ToList();
                var myTags = reader.ReadString().Split(",").ToList();
                var mutedTags = reader.ReadString().Split(",").ToList();
                var deafenedTags = reader.ReadString().Split(",").ToList();

                var voiceSettings = new VoiceSettings {
                    muteAll = muteAll,
                    mutedPeers = mutedPeers,
                    deafenAll = deafenAll,
                    deafenedPeers = deafenedPeers,
                    myTags = myTags,
                    mutedTags = mutedTags,
                    deafenedTags = deafenedTags
                };
                ClientVoiceSettings[clientId] = voiceSettings;
                OnClientVoiceSettingsUpdated?.Invoke();
            }
        }

        private void OnServerConnected(int connId)
        {
            Debug.unityLogger.Log(LogType.Log, TAG, $"Client {connId} connected");
            ClientIDs.Add(connId);
        }

        private void OnServerDisconnected(int connId)
        {
            ClientIDs.Remove(connId);
            Debug.unityLogger.Log(LogType.Log, TAG, $"Client {connId} disconnected");
        }

        private void SendToClient(int clientConnId, byte[] bytes, Channel channel)
        {
            if (!TryGetConnectionToClient(clientConnId, out var connection)) 
                return;
            
            var message = new FishNetBroadcast {data = bytes};
            _networkManager.ServerManager.Broadcast(connection, message, false, channel);
        }

        private bool TryGetConnectionToClient(int desiredClientID, out NetworkConnection resultConnection)
        {
            resultConnection = null;
            foreach (var (clientID, conn) in _networkManager.ServerManager.Clients)
            {
                if (clientID == desiredClientID)
                {
                    resultConnection = conn;
                    return true;
                } 
            }
            return false;
        }
    }
}
#endif
