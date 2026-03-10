#if UNITY_NETCODE_GAMEOBJECTS
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Adrenak.BRW;
using UnityEngine;
using System.Linq;

namespace Adrenak.UniVoice.Networks {
    /// <summary>
    /// This is the implementation of <see cref="IAudioClient{T}"/> interface for Netcode for GameObjects.
    /// It uses NGO CustomMessagingManager to send and receive UniVoice data to the server.
    /// Client IDs are cast from ulong to int for compatibility with VoiceSettings.
    /// </summary>
    public class NGOClient : IAudioClient<int> {
        private const string TAG = "[NGOClient]";
        private const string MESSAGE_NAME = "UniVoice_NGO";

        public int ID { get; private set; } = -1;

        public List<int> PeerIDs { get; private set; }

        public VoiceSettings YourVoiceSettings { get; private set; }

        public event Action<int, List<int>> OnJoined;
        public event Action OnLeft;
        public event Action<int> OnPeerJoined;
        public event Action<int> OnPeerLeft;
        public event Action<int, AudioFrame> OnReceivedPeerAudioFrame;

        private NetworkManager _networkManager;
        private NamedMessagePublisher _publisher;
        private bool _isDisposed;

        public NGOClient() {
            PeerIDs = new List<int>();
            YourVoiceSettings = new VoiceSettings();

            _networkManager = NetworkManager.Singleton;
            if (_networkManager == null) {
                Debug.LogError($"{TAG} NetworkManager.Singleton is null. Ensure Netcode for GameObjects is set up.");
                return;
            }

            _networkManager.OnClientConnectedCallback += OnClientConnected;
            _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public void Dispose() {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_networkManager != null) {
                _networkManager.OnClientConnectedCallback -= OnClientConnected;
                _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            PeerIDs.Clear();
        }

        private void OnClientConnected(ulong clientId) {
            if (!_networkManager.IsClient) return;

            // Client receives this when they connect; ID and peers come from server via PEER_INIT
            // No action needed - server will send PEER_INIT

            if (clientId != _networkManager.LocalClientId)
                return;

            if (_networkManager.CustomMessagingManager != null) {
                _publisher = _networkManager.CustomMessagingManager.GetPublisher();
                _publisher.Subscribe(MESSAGE_NAME, OnReceivedMessage);
            }
        }

        private void OnClientDisconnected(ulong clientId) {
            if (!_networkManager.IsClient) return;

            // Only process our own disconnection
            if (clientId != _networkManager.LocalClientId)
                return;

            if (_networkManager.CustomMessagingManager != null) {
                _publisher.Unsubscribe(MESSAGE_NAME, OnReceivedMessage);
            }

            OnClientDisconnectedLocal();
        }

        private void OnClientDisconnectedLocal() {
            YourVoiceSettings = new VoiceSettings();
            var oldPeerIds = new List<int>(PeerIDs);
            PeerIDs.Clear();
            ID = -1;
            foreach (var peerId in oldPeerIds)
                OnPeerLeft?.Invoke(peerId);
            OnLeft?.Invoke();
        }

        internal void OnReceivedMessage(ulong senderClientId, FastBufferReader reader) {
            // If we're not not a client, return
            if (!_networkManager.IsClient) return;

            if (senderClientId != 0) return;

            // Else, check if we're also a server which means our device is the host.
            // On a host, both NGOClient and NGOServer will end up having their OnReceivedMessage
            // handlers invoked. We need to dedupe this by early exiting when the message is
            // not from the server.
            //if (_networkManager.IsServer && senderClientId != _networkManager.LocalClientId) {
            //    Debug.unityLogger.Log(TAG, $"OnReceivedMessage from " + senderClientId + ". But local is " + _networkManager.LocalClientId);
            //    return;
            //}

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

            switch (tag) {
                case NGOMessageTags.PEER_INIT:
                    ID = msgReader.ReadInt();
                    PeerIDs = msgReader.ReadIntArray().ToList();

                    var log = $"Initialized with ID {ID}. ";
                    if (PeerIDs.Count > 0)
                        log += $"Peer list: {string.Join(", ", PeerIDs)}";
                    else
                        log += "There are currently no peers.";
                    Debug.unityLogger.Log(LogType.Log, TAG, log);

                    OnJoined?.Invoke(ID, PeerIDs);
                    foreach (var peerId in PeerIDs)
                        OnPeerJoined?.Invoke(peerId);
                    break;

                case NGOMessageTags.PEER_JOINED:
                    var newPeerID = msgReader.ReadInt();
                    if (!PeerIDs.Contains(newPeerID)) {
                        PeerIDs.Add(newPeerID);
                        Debug.unityLogger.Log(LogType.Log, TAG,
                            $"Peer {newPeerID} joined. Peer list is now {string.Join(", ", PeerIDs)}");
                        OnPeerJoined?.Invoke(newPeerID);
                    }
                    break;

                case NGOMessageTags.PEER_LEFT:
                    var leftPeerID = msgReader.ReadInt();
                    if (PeerIDs.Contains(leftPeerID)) {
                        PeerIDs.Remove(leftPeerID);
                        var log2 = $"Peer {leftPeerID} left. ";
                        if (PeerIDs.Count == 0)
                            log2 += "There are no peers anymore.";
                        else
                            log2 += $"Peer list is now {string.Join(", ", PeerIDs)}";

                        Debug.unityLogger.Log(LogType.Log, TAG, log2);
                        OnPeerLeft?.Invoke(leftPeerID);
                    }
                    break;

                case NGOMessageTags.AUDIO_FRAME:
                    var sender = msgReader.ReadInt();

                    if (sender == ID || !PeerIDs.Contains(sender))
                        return;
                    var frame = new AudioFrame {
                        timestamp = msgReader.ReadLong(),
                        frequency = msgReader.ReadInt(),
                        channelCount = msgReader.ReadInt(),
                        samples = msgReader.ReadByteArray()
                    };

                    OnReceivedPeerAudioFrame?.Invoke(sender, frame);
                    break;
            }
        }

        public void SendAudioFrame(AudioFrame frame) {
            if (ID == -1 || _networkManager == null || !_networkManager.IsClient)
                return;

            var writer = new BytesWriter();
            writer.WriteString(NGOMessageTags.AUDIO_FRAME);
            writer.WriteInt(ID);
            writer.WriteLong(frame.timestamp);
            writer.WriteInt(frame.frequency);
            writer.WriteInt(frame.channelCount);
            writer.WriteByteArray(frame.samples);

            SendToServer(writer.Bytes, NetworkDelivery.Unreliable);
        }

        public void SubmitVoiceSettings() {
            if (ID == -1 || _networkManager == null || !_networkManager.IsClient)
                return;

            Debug.unityLogger.Log(TAG, "Submitting : " + YourVoiceSettings);

            var writer = new BytesWriter();
            writer.WriteString(NGOMessageTags.VOICE_SETTINGS);
            writer.WriteInt(YourVoiceSettings.muteAll ? 1 : 0);
            writer.WriteIntArray(YourVoiceSettings.mutedPeers.ToArray());
            writer.WriteInt(YourVoiceSettings.deafenAll ? 1 : 0);
            writer.WriteIntArray(YourVoiceSettings.deafenedPeers.ToArray());
            writer.WriteStringArray(YourVoiceSettings.myTags.ToArray());
            writer.WriteStringArray(YourVoiceSettings.mutedTags.ToArray());
            writer.WriteStringArray(YourVoiceSettings.deafenedTags.ToArray());

            SendToServer(writer.Bytes, NetworkDelivery.Reliable);
        }

        public void UpdateVoiceSettings(Action<VoiceSettings> modification) {
            modification?.Invoke(YourVoiceSettings);
            SubmitVoiceSettings();
        }

        private void SendToServer(byte[] data, NetworkDelivery delivery) {
            if (_networkManager == null || !_networkManager.IsClient)
                return;

            var writer = new FastBufferWriter(data.Length + 4, Allocator.Temp);
            try {
                writer.WriteValueSafe(data.Length);
                writer.WriteBytesSafe(data);
                if (_networkManager.CustomMessagingManager != null)
                    _networkManager.CustomMessagingManager.SendNamedMessage(MESSAGE_NAME, NetworkManager.ServerClientId, writer, delivery);
            }
            catch (Exception e) {
                Debug.unityLogger.LogError(TAG, e);
            }
            finally {
                writer.Dispose();
            }
        }
    }
}
#endif
