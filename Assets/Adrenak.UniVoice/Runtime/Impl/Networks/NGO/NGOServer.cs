#if UNITY_NETCODE_GAMEOBJECTS
using System.Linq;
using System.Threading.Tasks;
using Adrenak.BRW;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// Netcode for GameObjects implementation of <see cref="IAudioServer{T}"/>.
    /// On top of the shared forwarding protocol inherited from <see cref="AAudioServerBase"/>,
    /// NGO also drives the PEER_INIT / PEER_JOINED / PEER_LEFT notifications consumed by
    /// <see cref="NGOClient"/> (NGO doesn't expose remote-client lifecycle to other clients
    /// natively). Client ids (ulong) are cast to int.
    /// </summary>
    public class NGOServer : AAudioServerBase
    {
        protected sealed override string Tag => "[NGOServer]";

        private readonly NetworkManager _networkManager;
        private readonly NGOClient _localClient;
        private NamedMessagePublisher _publisher;
        private bool _serverStarted;

        public NGOServer(NGOClient localClient)
        {
            _localClient = localClient;

            _networkManager = NetworkManager.Singleton;
            if (_networkManager == null)
            {
                Debug.LogError($"{Tag} NetworkManager.Singleton is null. Ensure Netcode for GameObjects is set up.");
                return;
            }

            _networkManager.OnServerStarted += OnNetworkServerStarted;
            _networkManager.OnServerStopped += OnNetworkServerStopped;
            _networkManager.OnClientConnectedCallback += OnNetworkClientConnected;
            _networkManager.OnClientDisconnectCallback += OnNetworkClientDisconnected;
        }

        public override void Dispose()
        {
            if (_networkManager != null)
            {
                _networkManager.OnServerStarted -= OnNetworkServerStarted;
                _networkManager.OnServerStopped -= OnNetworkServerStopped;
                _networkManager.OnClientConnectedCallback -= OnNetworkClientConnected;
                _networkManager.OnClientDisconnectCallback -= OnNetworkClientDisconnected;
            }
            UnsubscribeNamedMessage();
            base.Dispose();
        }

        protected override void SendToClient(int clientId, byte[] data, bool reliable)
        {
            // Host-loopback: feed directly into the local client to avoid round-tripping through NGO.
            if (clientId == 0)
            {
                _localClient?.HandleLoopbackBytes(data);
                return;
            }

            if (_networkManager == null || !_networkManager.IsServer || _networkManager.CustomMessagingManager == null)
                return;

            var writer = new FastBufferWriter(data.Length + 4, Allocator.Temp);
            try
            {
                writer.WriteValueSafe(data.Length);
                writer.WriteBytesSafe(data);
                _networkManager.CustomMessagingManager.SendNamedMessage(
                    NGOMessageTags.MESSAGE_NAME,
                    (ulong) clientId,
                    writer,
                    reliable ? NetworkDelivery.Reliable : NetworkDelivery.Unreliable);
            }
            finally
            {
                writer.Dispose();
            }
        }

        protected override void OnAfterClientConnected(int clientId)
        {
            foreach (var peer in ClientIDs)
            {
                if (peer == clientId)
                {
                    var otherPeerIDs = ClientIDs.Where(x => x != clientId).ToArray();
                    var packet = new BytesWriter()
                        .WriteString(NGOMessageTags.PEER_INIT)
                        .WriteInt(clientId)
                        .WriteIntArray(otherPeerIDs);

                    // Slight delay — immediate init has been observed to occasionally race.
                    SendToClientDelayed(clientId, packet.Bytes, reliable: true, 100);

                    var log = $"Initializing new client with ID {clientId}";
                    if (otherPeerIDs.Length > 0)
                        log += $" and peer list {string.Join(", ", otherPeerIDs)}";
                    Debug.unityLogger.Log(LogType.Log, Tag, log);
                }
                else
                {
                    var packet = new BytesWriter()
                        .WriteString(NGOMessageTags.PEER_JOINED)
                        .WriteInt(clientId);
                    Debug.unityLogger.Log(LogType.Log, Tag,
                        $"Notified client {peer} about new client {clientId}");
                    SendToClient(peer, packet.Bytes, reliable: true);
                }
            }
        }

        protected override void OnAfterClientDisconnected(int clientId)
        {
            foreach (var peerId in ClientIDs)
            {
                var packet = new BytesWriter()
                    .WriteString(NGOMessageTags.PEER_LEFT)
                    .WriteInt(clientId);
                Debug.unityLogger.Log(LogType.Log, Tag,
                    $"Notified client {peerId} about {clientId} leaving");
                SendToClient(peerId, packet.Bytes, reliable: true);
            }
        }

        private void OnNetworkServerStarted()
        {
            if (_serverStarted) return;
            _serverStarted = true;
            SubscribeNamedMessage();
            RaiseServerStarted();
        }

        private void OnNetworkServerStopped(bool wasHost)
        {
            if (!_serverStarted) return;
            _serverStarted = false;
            UnsubscribeNamedMessage();
            RaiseServerStopped();
        }

        private void OnNetworkClientConnected(ulong clientId)
        {
            if (!_networkManager.IsServer) return;
            HandleClientConnected((int) clientId);
        }

        private void OnNetworkClientDisconnected(ulong clientId)
        {
            if (!_networkManager.IsServer) return;
            HandleClientDisconnected((int) clientId);
        }

        private void OnReceivedNamedMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!_networkManager.IsServer) return;
            if (!TryUnwrapPayload(reader, out var payload)) return;
            HandleIncomingPayload((int) senderClientId, payload);
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

        private async void SendToClientDelayed(int clientId, byte[] data, bool reliable, int delayMs)
        {
            await Task.Delay(delayMs);
            SendToClient(clientId, data, reliable);
        }
    }
}
#endif
