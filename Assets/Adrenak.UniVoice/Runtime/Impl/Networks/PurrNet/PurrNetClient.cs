#define PURRNET
#if PURRNET
using System;
using Adrenak.BRW;
using EDIVE.Audio;
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

        protected override void WriteId(BytesWriter writer, PlayerID id) => writer.WritePlayerID(id);
        protected override PlayerID ReadId(BytesReader reader) => reader.ReadPlayerID();

        private void OnLocalPlayerReceivedID(PlayerID player)
        {
            ID = player;
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
}
#endif
