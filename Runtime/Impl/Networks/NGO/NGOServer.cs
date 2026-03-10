#if UNITY_NETCODE_GAMEOBJECTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Adrenak.BRW;
using UnityEngine;
using System.Security.Cryptography;

namespace Adrenak.UniVoice.Networks {
    /// <summary>
    /// This is an implementation of the <see cref="IAudioServer{T}"/> interface for Netcode for GameObjects.
    /// It uses NGO CustomMessagingManager to send and receive UniVoice audio data to and from clients.
    /// Client IDs (ulong) are cast to int for compatibility with VoiceSettings.
    /// </summary>
    public class NGOServer : IAudioServer<int> {
        private const string TAG = "[NGOServer]";
        private const string MESSAGE_NAME = "UniVoice_NGO";

        public event Action OnServerStart;
        public event Action OnServerStop;
        public event Action OnClientVoiceSettingsUpdated;

        public List<int> ClientIDs { get; private set; }
        public Dictionary<int, VoiceSettings> ClientVoiceSettings { get; private set; }

        private NetworkManager _networkManager;
        private NamedMessagePublisher _publisher;
        private bool _isServerStarted;
        private bool _isDisposed;
        private NGOClient _localClient;

        public NGOServer(NGOClient localClient) {
            _localClient = localClient;
            ClientIDs = new List<int>();
            ClientVoiceSettings = new Dictionary<int, VoiceSettings>();

            _networkManager = NetworkManager.Singleton;
            if (_networkManager == null) {
                Debug.LogError($"{TAG} NetworkManager.Singleton is null. Ensure Netcode for GameObjects is set up.");
                return;
            }

            _networkManager.OnClientConnectedCallback += OnServerClientConnected;
            _networkManager.OnClientDisconnectCallback += OnServerClientDisconnected;
            _networkManager.OnServerStarted += OnServerStarted;
            _networkManager.OnServerStopped += OnServerShutdown;
        }

        private void OnServerShutdown(bool wasHost) {
            OnServerShutdown();
        }

        public void Dispose() {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_networkManager != null) {
                _networkManager.OnClientConnectedCallback -= OnServerClientConnected;
                _networkManager.OnClientDisconnectCallback -= OnServerClientDisconnected;
                _networkManager.OnServerStarted -= OnServerStarted;
                _networkManager.OnServerStopped -= OnServerShutdown;
            }
            OnServerShutdown();
        }

        private void OnServerStarted() {
            if (_isServerStarted) return;
            _isServerStarted = true;

            if (_networkManager.CustomMessagingManager != null) {
                _publisher = _networkManager.CustomMessagingManager.GetPublisher();
                _publisher.Subscribe(MESSAGE_NAME, OnReceivedMessage);
            }

            OnServerStart?.Invoke();
        }

        private void OnServerShutdown() {
            if (_networkManager.CustomMessagingManager != null) {
                _publisher.Unsubscribe(MESSAGE_NAME, OnReceivedMessage);
            }

            _isServerStarted = false;
            ClientIDs.Clear();
            ClientVoiceSettings.Clear();

            OnServerStop?.Invoke();
        }

        private void OnServerClientConnected(ulong clientId) {
            if (!_networkManager.IsServer) return;

            var connId = (int)clientId;
            if (ClientIDs.Contains(connId)) return;

            OnServerStarted();

            ClientIDs.Add(connId);
            Debug.unityLogger.Log(LogType.Log, TAG, $"Client {connId} connected. IDs now: {string.Join(", ", ClientIDs)}");

            foreach (var peerId in ClientIDs) {
                if (peerId == connId) {
                    var otherPeerIDs = ClientIDs.Where(x => x != connId).ToArray();
                    var writer = new BytesWriter()
                        .WriteString(NGOMessageTags.PEER_INIT)
                        .WriteInt(connId)
                        .WriteIntArray(otherPeerIDs);

                    var log = $"Initializing new client with ID {connId}";
                    if (otherPeerIDs.Length > 0)
                        log += $" and peer list {string.Join(", ", otherPeerIDs)}";

                    Debug.unityLogger.Log(LogType.Log, TAG, log);

                    SendToClientDelayed(connId, writer.Bytes, NetworkDelivery.Reliable, 100);
                }
                else {
                    var writer = new BytesWriter()
                        .WriteString(NGOMessageTags.PEER_JOINED)
                        .WriteInt(connId);
                    Debug.unityLogger.Log(LogType.Log, TAG, $"Notified client {peerId} about new client {connId}");
                    SendToClient(peerId, writer.Bytes, NetworkDelivery.Reliable);
                }
            }
        }

        private void OnServerClientDisconnected(ulong clientId) {
            if (!_networkManager.IsServer) return;

            var connId = (int)clientId;
            ClientIDs.Remove(connId);
            ClientVoiceSettings.Remove(connId);
            Debug.unityLogger.Log(LogType.Log, TAG, $"Client {connId} disconnected. IDs now: {string.Join(", ", ClientIDs)}");

            foreach (var peerId in ClientIDs) {
                var writer = new BytesWriter()
                    .WriteString(NGOMessageTags.PEER_LEFT)
                    .WriteInt(connId);
                Debug.unityLogger.Log(LogType.Log, TAG, $"Notified client {peerId} about {connId} leaving");
                SendToClient(peerId, writer.Bytes, NetworkDelivery.Reliable);
            }

            if (ClientIDs.Count == 0)
                OnServerShutdown();
        }

        private void OnReceivedMessage(ulong senderClientId, FastBufferReader reader) {
            if (!_networkManager.IsServer) return;

            var clientId = (int)senderClientId;
            var length = reader.Length;
            if (length <= 0) return;

            if (!reader.TryBeginRead(4)) return;
            reader.ReadValueSafe(out int payloadLength);

            if (!reader.TryBeginRead(payloadLength)) {
                Debug.LogError($"{TAG} TryBeginRead failed - not enough data in buffer. Needed {payloadLength}");
                return;
            }

            var data = new byte[payloadLength];
            reader.ReadBytes(ref data, payloadLength);

            var msgReader = new BytesReader(data);
            var tag = msgReader.ReadString();

            if (tag.Equals(NGOMessageTags.AUDIO_FRAME)) {
                var peersToForwardAudioTo = ClientIDs.Where(x => x != clientId);

                if (ClientVoiceSettings.TryGetValue(clientId, out var senderSettings)) {
                    if (senderSettings.deafenAll)
                        return;

                    peersToForwardAudioTo = peersToForwardAudioTo
                        .Where(x => !senderSettings.deafenedPeers.Contains(x));

                    peersToForwardAudioTo = peersToForwardAudioTo.Where(peer => {
                        if (ClientVoiceSettings.TryGetValue(peer, out var peerVoiceSettings)) {
                            var hasDeafenedPeer = senderSettings.deafenedTags.Intersect(peerVoiceSettings.myTags).Any();
                            return !hasDeafenedPeer;
                        }
                        return true;
                    });
                }

                var sender = msgReader.ReadInt();
                var frame = new AudioFrame {
                    timestamp = msgReader.ReadLong(),
                    frequency = msgReader.ReadInt(),
                    channelCount = msgReader.ReadInt(),
                    samples = msgReader.ReadByteArray()
                };

                foreach (var recipient in peersToForwardAudioTo) {
                    if (ClientVoiceSettings.TryGetValue(recipient, out var recipientSettings)) {
                        if (recipientSettings.muteAll)
                            continue;
                        if (recipientSettings.mutedPeers.Contains(clientId))
                            continue;
                        if (senderSettings != null && recipientSettings.mutedTags.Intersect(senderSettings.myTags).Any())
                            continue;
                    }
                    SendToClient(recipient, data, NetworkDelivery.Unreliable);
                }
            }
            else if (tag.Equals(NGOMessageTags.VOICE_SETTINGS)) {
                var muteAll = msgReader.ReadInt() == 1;
                var mutedPeers = msgReader.ReadIntArray().ToList();
                var deafenAll = msgReader.ReadInt() == 1;
                var deafenedPeers = msgReader.ReadIntArray().ToList();
                var myTags = msgReader.ReadStringArray().ToList();
                var mutedTags = msgReader.ReadStringArray().ToList();
                var deafenedTags = msgReader.ReadStringArray().ToList();

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

        private async void SendToClientDelayed(int clientId, byte[] bytes, NetworkDelivery delivery, int delayMs) {
            await Task.Delay(delayMs);
            SendToClient(clientId, bytes, delivery);
        }

        private void SendToClient(int clientId, byte[] bytes, NetworkDelivery delivery) {
            if (_networkManager == null || !_networkManager.IsServer) return;

            var writer = new FastBufferWriter(bytes.Length + 4, Allocator.Temp);
            try {
                writer.WriteValueSafe(bytes.Length);
                writer.WriteBytesSafe(bytes);
                if (clientId == 0)
                    _localClient.OnReceivedMessage(0, new FastBufferReader(writer, Allocator.Temp));
                else if (_networkManager.CustomMessagingManager != null)
                    _networkManager.CustomMessagingManager.SendNamedMessage(MESSAGE_NAME, (ulong)clientId, writer, delivery);
            }
            finally {
                writer.Dispose();
            }
        }
    }
}
#endif
