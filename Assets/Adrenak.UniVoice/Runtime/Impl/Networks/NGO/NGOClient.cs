#if UNITY_NETCODE_GAMEOBJECTS
using Adrenak.BRW;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// Netcode for GameObjects implementation of <see cref="IAudioClient{T}"/>.
    /// NGO does not expose remote-client lifecycle events to peers natively, so the
    /// protocol is augmented with the PEER_INIT / PEER_JOINED / PEER_LEFT tags driven
    /// by <see cref="NGOServer"/>. Everything else is inherited from <see cref="AAudioClientBase{T}"/>.
    /// Client ids are cast from ulong to int for compatibility with <see cref="VoiceSettings"/>.
    /// </summary>
    public class NGOClient : AAudioClientBase<int>
    {
        protected sealed override string Tag => "[NGOClient]";
        protected override bool HasLocalId => ID != -1;
        protected override bool IsConnected => _networkManager != null && _networkManager.IsClient;

        private readonly NetworkManager _networkManager;
        private NamedMessagePublisher _publisher;

        public NGOClient()
        {
            ID = -1;
            _networkManager = NetworkManager.Singleton;
            if (_networkManager == null)
            {
                Debug.LogError($"{Tag} NetworkManager.Singleton is null. Ensure Netcode for GameObjects is set up.");
                return;
            }

            _networkManager.OnClientConnectedCallback += OnClientConnected;
            _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void Dispose()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientConnectedCallback -= OnClientConnected;
                _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            UnsubscribeNamedMessage();
            base.Dispose();
        }

        protected override void ResetLocalId() => ID = -1;

        protected override void SendToServer(byte[] data, bool reliable)
        {
            if (_networkManager == null || !_networkManager.IsClient || _networkManager.CustomMessagingManager == null)
                return;

            var writer = new FastBufferWriter(data.Length + 4, Allocator.Temp);
            try
            {
                writer.WriteValueSafe(data.Length);
                writer.WriteBytesSafe(data);
                _networkManager.CustomMessagingManager.SendNamedMessage(
                    NGOMessageTags.MESSAGE_NAME,
                    NetworkManager.ServerClientId,
                    writer,
                    reliable ? NetworkDelivery.Reliable : NetworkDelivery.Unreliable);
            }
            finally
            {
                writer.Dispose();
            }
        }

        protected override void WriteId(BytesWriter writer, int id) => writer.WriteInt(id);
        protected override int ReadId(BytesReader reader) => reader.ReadInt();

        protected override void DispatchIncomingTag(string tag, BytesReader reader)
        {
            switch (tag)
            {
                case NGOMessageTags.PEER_INIT:
                {
                    var localId = reader.ReadInt();
                    var existingPeers = reader.ReadIntArray();
                    HandleLocalJoined(localId, existingPeers);
                    break;
                }
                case NGOMessageTags.PEER_JOINED:
                    HandlePeerJoined(reader.ReadInt());
                    break;
                case NGOMessageTags.PEER_LEFT:
                    HandlePeerLeft(reader.ReadInt());
                    break;
                default:
                    base.DispatchIncomingTag(tag, reader);
                    break;
            }
        }

        /// <summary>
        /// Host-loopback entrypoint used by <see cref="NGOServer"/>: lets the in-process
        /// server feed bytes straight into this client without going through NGO transport.
        /// </summary>
        internal void HandleLoopbackBytes(byte[] data) => HandleIncomingPayload(data);

        private void OnClientConnected(ulong clientId)
        {
            if (!_networkManager.IsClient) return;
            if (clientId != _networkManager.LocalClientId) return;

            // Local id and peer roster come from the server's PEER_INIT — wait for that to fire HandleLocalJoined.
            SubscribeNamedMessage();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!_networkManager.IsClient) return;
            if (clientId != _networkManager.LocalClientId) return;

            UnsubscribeNamedMessage();
            HandleLocalLeft();
        }

        private void OnReceivedNamedMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!_networkManager.IsClient) return;
            // Only accept messages routed by the server (server id is 0 in NGO).
            if (senderClientId != NetworkManager.ServerClientId) return;

            if (!TryUnwrapPayload(reader, out var payload)) return;
            HandleIncomingPayload(payload);
        }

        private static bool TryUnwrapPayload(FastBufferReader reader, out byte[] payload)
        {
            payload = null;
            if (reader.Length <= 0) return false;
            if (!reader.TryBeginRead(4)) return false;

            reader.ReadValueSafe(out int payloadLength);
            if (!reader.TryBeginRead(payloadLength)) return false;

            payload = new byte[payloadLength];
            reader.ReadBytes(ref payload, payloadLength);
            return true;
        }

        private void SubscribeNamedMessage()
        {
            if (_publisher != null) return;
            if (_networkManager.CustomMessagingManager == null) return;

            _publisher = _networkManager.CustomMessagingManager.GetPublisher();
            _publisher.Subscribe(NGOMessageTags.MESSAGE_NAME, OnReceivedNamedMessage);
        }

        private void UnsubscribeNamedMessage()
        {
            if (_publisher == null) return;
            _publisher.Unsubscribe(NGOMessageTags.MESSAGE_NAME, OnReceivedNamedMessage);
            _publisher = null;
        }
    }
}
#endif
