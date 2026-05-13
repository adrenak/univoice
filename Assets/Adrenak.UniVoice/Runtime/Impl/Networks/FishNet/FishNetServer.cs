#if FISHNET
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// FishNet implementation of <see cref="IAudioServer{T}"/>.
    /// Inherits the forwarding/filtering protocol from <see cref="AAudioServerBase"/>;
    /// only the framework wiring lives here.
    /// </summary>
    public class FishNetServer : AAudioServerBase
    {
        protected override string Tag => "[FishNetServer]";

        private readonly NetworkManager _networkManager;

        public FishNetServer()
        {
            _networkManager = InstanceFinder.NetworkManager;
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionStateChanged;
            _networkManager.ServerManager.OnRemoteConnectionState += OnServerRemoteConnectionStateChanged;
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionStateChanged;
            _networkManager.ServerManager.RegisterBroadcast<FishNetBroadcast>(OnReceivedMessage, false);
        }

        public override void Dispose()
        {
            if (_networkManager)
            {
                _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionStateChanged;
                _networkManager.ServerManager.OnRemoteConnectionState -= OnServerRemoteConnectionStateChanged;
                _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionStateChanged;
                _networkManager.ServerManager.UnregisterBroadcast<FishNetBroadcast>(OnReceivedMessage);
            }
            base.Dispose();
        }

        protected override void SendToClient(int clientId, byte[] data, bool reliable)
        {
            if (!TryGetConnectionToClient(clientId, out var connection)) return;
            var message = new FishNetBroadcast { data = data };
            _networkManager.ServerManager.Broadcast(connection, message, false,
                reliable ? Channel.Reliable : Channel.Unreliable);
        }

        private void OnServerRemoteConnectionStateChanged(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
                HandleClientConnected(connection.ClientId);
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
                HandleClientDisconnected(connection.ClientId);
        }

        private void OnServerConnectionStateChanged(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started && InstanceFinder.ServerManager.IsOnlyOneServerStarted())
                RaiseServerStarted();
            else if (args.ConnectionState == LocalConnectionState.Stopped && !InstanceFinder.ServerManager.IsAnyServerStarted()) 
                RaiseServerStopped();
        }

        private void OnClientConnectionStateChanged(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                HandleClientConnected(0);
            else if (args.ConnectionState == LocalConnectionState.Stopped)
                HandleClientDisconnected(0);
        }

        private void OnReceivedMessage(NetworkConnection connection, FishNetBroadcast message, Channel channel)
        {
            HandleIncomingPayload(connection.ClientId, message.data);
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
