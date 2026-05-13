#if FISHNET
using System.Linq;
using Adrenak.BRW;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// FishNet implementation of <see cref="IAudioClient{T}"/>.
    /// Inherits the BRW protocol and peer bookkeeping from <see cref="AAudioClientBase{T}"/>;
    /// only the framework wiring lives here.
    /// </summary>
    public class FishNetClient : AAudioClientBase<int>
    {
        protected sealed override string Tag => "[FishNetClient]";
        protected override bool HasLocalId => ID != -1;
        protected override bool IsConnected => _networkManager != null && _networkManager.ClientManager.Started;

        private readonly NetworkManager _networkManager;

        public FishNetClient()
        {
            ID = -1;
            _networkManager = InstanceFinder.NetworkManager;
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionStateChanged;
            _networkManager.ClientManager.OnAuthenticated += OnClientAuthenticated;
            _networkManager.ClientManager.OnRemoteConnectionState += OnRemoteConnectionStateChanged;
            _networkManager.ClientManager.RegisterBroadcast<FishNetBroadcast>(OnReceivedMessage);
        }

        public override void Dispose()
        {
            if (_networkManager != null)
            {
                _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionStateChanged;
                _networkManager.ClientManager.OnAuthenticated -= OnClientAuthenticated;
                _networkManager.ClientManager.OnRemoteConnectionState -= OnRemoteConnectionStateChanged;
                _networkManager.ClientManager.UnregisterBroadcast<FishNetBroadcast>(OnReceivedMessage);
            }
            base.Dispose();
        }

        protected override void ResetLocalId() => ID = -1;

        protected override void SendToServer(byte[] data, bool reliable)
        {
            var message = new FishNetBroadcast { data = data };
            _networkManager.ClientManager.Broadcast(message, reliable ? Channel.Reliable : Channel.Unreliable);
        }

        protected override void WriteId(BytesWriter writer, int id) => writer.WriteInt(id);
        protected override int ReadId(BytesReader reader) => reader.ReadInt();

        private void OnRemoteConnectionStateChanged(RemoteConnectionStateArgs args)
        {
            // Don't process connection state changes before the client is authenticated
            if (_networkManager.ClientManager.Connection.ClientId < 0)
                return;

            if (args.ConnectionState == RemoteConnectionState.Started)
                HandlePeerJoined(args.ConnectionId);
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
                HandlePeerLeft(args.ConnectionId);
        }

        // We use OnClientAuthenticated rather than OnClientConnectionState because ClientId is only set after auth.
        private void OnClientAuthenticated()
        {
            var localId = _networkManager.ClientManager.Connection.ClientId;
            var existingPeers = _networkManager.ClientManager.Clients.Keys.Where(x => x != localId);
            HandleLocalJoined(localId, existingPeers);
        }

        private void OnClientConnectionStateChanged(ClientConnectionStateArgs args)
        {
            // Only handle stopped here — started is handled in OnClientAuthenticated.
            if (args.ConnectionState == LocalConnectionState.Stopped)
                HandleLocalLeft();
        }

        private void OnReceivedMessage(FishNetBroadcast msg, Channel channel)
        {
            HandleIncomingPayload(msg.data);
        }
    }
}
#endif
