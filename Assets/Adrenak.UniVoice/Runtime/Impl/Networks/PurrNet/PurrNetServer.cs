#if PURRNET
using PurrNet;
using PurrNet.Transports;
using UnityEngine;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// PurrNet implementation of <see cref="IAudioServer{T}"/>.
    /// Inherits the forwarding/filtering protocol from <see cref="AAudioServerBase"/>;
    /// only the framework wiring lives here.
    /// </summary>
    public class PurrNetServer : AAudioServerBase
    {
        protected sealed override string Tag => "[PurrNetServer]";

        private readonly NetworkManager _networkManager;
        private bool _serverStarted;

        public PurrNetServer()
        {
            _networkManager = NetworkManager.main;
            if (_networkManager == null)
            {
                Debug.unityLogger.LogError(Tag, "NetworkManager.main is null. PurrNetServer cannot subscribe.");
                return;
            }

            _networkManager.onServerConnectionState += OnServerConnectionStateChanged;
            _networkManager.onPlayerJoined += OnPlayerJoined;
            _networkManager.onPlayerLeft += OnPlayerLeft;
            _networkManager.Subscribe<PurrNetBroadcast>(OnReceivedMessage, asServer: true);
        }

        public override void Dispose()
        {
            if (_networkManager)
            {
                _networkManager.onServerConnectionState -= OnServerConnectionStateChanged;
                _networkManager.onPlayerJoined -= OnPlayerJoined;
                _networkManager.onPlayerLeft -= OnPlayerLeft;
                _networkManager.Unsubscribe<PurrNetBroadcast>(OnReceivedMessage, asServer: true);
            }
            base.Dispose();
        }

        protected override void SendToClient(int clientId, byte[] data, bool reliable)
        {
            if (!TryGetPlayer(clientId, out var player)) return;
            var message = new PurrNetBroadcast { data = data };
            _networkManager.Send(player, message, reliable ? Channel.ReliableOrdered : Channel.Unreliable);
        }

        private void OnServerConnectionStateChanged(ConnectionState state)
        {
            if (state == ConnectionState.Connected)
            {
                if (_serverStarted) return;
                _serverStarted = true;
                RaiseServerStarted();
            }
            else if (state == ConnectionState.Disconnected)
            {
                if (!_serverStarted) return;
                _serverStarted = false;
                RaiseServerStopped();
            }
        }

        private void OnPlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            if (!asServer) return;
            HandleClientConnected(PlayerIdToInt(player));
        }

        private void OnPlayerLeft(PlayerID player, bool asServer)
        {
            if (!asServer) return;
            HandleClientDisconnected(PlayerIdToInt(player));
        }

        private void OnReceivedMessage(PlayerID sender, PurrNetBroadcast message, bool asServer)
        {
            if (!asServer) return;
            HandleIncomingPayload(PlayerIdToInt(sender), message.data);
        }

        private bool TryGetPlayer(int desiredClientID, out PlayerID resultPlayer)
        {
            foreach (var p in _networkManager.players)
            {
                if (PlayerIdToInt(p) == desiredClientID)
                {
                    resultPlayer = p;
                    return true;
                }
            }
            resultPlayer = default;
            return false;
        }

        private static int PlayerIdToInt(PlayerID id) => (int) id.id.value;
    }
}
#endif
