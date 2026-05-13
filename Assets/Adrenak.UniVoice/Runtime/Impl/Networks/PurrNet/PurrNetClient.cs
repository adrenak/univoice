#if PURRNET
using System;
using Adrenak.BRW;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// PurrNet implementation of <see cref="IAudioClient{T}"/>.
    /// Inherits the BRW protocol and peer bookkeeping from <see cref="AAudioClientBase{T}"/>;
    /// only the framework wiring lives here.
    /// </summary>
    public class PurrNetClient : AAudioClientBase<PlayerID>
    {
        protected sealed override string Tag => "[PurrNetClient]";
        protected override bool HasLocalId => _hasLocalId;
        protected override bool IsConnected => _networkManager != null && _networkManager.isClient;

        private bool _hasLocalId;
        private readonly NetworkManager _networkManager;

        public PurrNetClient()
        {
            _networkManager = NetworkManager.main;
            if (_networkManager == null)
            {
                Debug.unityLogger.LogError(Tag, "NetworkManager.main is null. PurrNetClient cannot subscribe.");
                return;
            }

            _networkManager.onClientConnectionState += OnClientConnectionStateChanged;
            _networkManager.onLocalPlayerReceivedID += OnLocalPlayerReceivedID;
            _networkManager.onPlayerJoined += OnPlayerJoined;
            _networkManager.onPlayerLeft += OnPlayerLeft;
            _networkManager.Subscribe<PurrNetBroadcast>(OnReceivedMessage, asServer: false);
        }

        public override void Dispose()
        {
            if (_networkManager != null)
            {
                _networkManager.onClientConnectionState -= OnClientConnectionStateChanged;
                _networkManager.onLocalPlayerReceivedID -= OnLocalPlayerReceivedID;
                _networkManager.onPlayerJoined -= OnPlayerJoined;
                _networkManager.onPlayerLeft -= OnPlayerLeft;
                _networkManager.Unsubscribe<PurrNetBroadcast>(OnReceivedMessage, asServer: false);
            }
            base.Dispose();
        }

        protected override void ResetLocalId()
        {
            ID = default;
            _hasLocalId = false;
        }

        protected override void SendToServer(byte[] data, bool reliable)
        {
            var message = new PurrNetBroadcast { data = data };
            _networkManager.SendToServer(message, reliable ? Channel.ReliableOrdered : Channel.Unreliable);
        }

        protected override void WriteId(BytesWriter writer, PlayerID id)
        {
            var bytes = BitConverter.GetBytes(id.id);
            EndianUtility.EndianCorrection(bytes);
            writer.WriteBytes(bytes);
            writer.WriteByte(id.isBot ? (byte) 1 : (byte) 0);
        }

        protected override PlayerID ReadId(BytesReader reader)
        {
            var idBytes = reader.ReadBytes(8);
            var isBot = reader.ReadBytes(1)[0] == 1;
            EndianUtility.EndianCorrection(idBytes);
            return new PlayerID(BitConverter.ToUInt64(idBytes, 0), isBot);
        }

        private void OnLocalPlayerReceivedID(PlayerID player)
        {
            _hasLocalId = true;
            HandleLocalJoined(player, _networkManager.players);
        }

        private void OnPlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            // Only react to the client-side view; ignore server-side mirrors of the event.
            if (asServer) return;
            // Until we know our own id we cannot tell which join refers to ourselves.
            if (!_hasLocalId) return;
            HandlePeerJoined(player);
        }

        private void OnPlayerLeft(PlayerID player, bool asServer)
        {
            if (asServer) return;
            HandlePeerLeft(player);
        }

        private void OnClientConnectionStateChanged(ConnectionState state)
        {
            if (state != ConnectionState.Disconnected) return;
            HandleLocalLeft();
        }

        private void OnReceivedMessage(PlayerID sender, PurrNetBroadcast msg, bool asServer) =>
            HandleIncomingPayload(msg.data);
    }
#endif
}
