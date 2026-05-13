// Notes:
// In Mirror 89.11.0, the OnServerConnectedWithAddress event was added.
// https://github.com/MirrorNetworking/Mirror/releases/tag/v89.11.0
// OnServerConnected no longer seems to work?

#if MIRROR
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Mirror;
using Adrenak.BRW;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// Mirror implementation of <see cref="IAudioServer{T}"/>.
    /// On top of the shared forwarding protocol inherited from <see cref="AudioServerBase"/>,
    /// Mirror also drives the PEER_INIT / PEER_JOINED / PEER_LEFT notifications consumed
    /// by <see cref="MirrorClient"/> (Mirror's transport doesn't expose remote-client
    /// lifecycle to other clients natively).
    /// </summary>
    public class MirrorServer : AudioServerBase
    {
        protected sealed override string Tag => "[MirrorServer]";

        private readonly MirrorModeObserver _mirrorEvents;

        public MirrorServer()
        {
            _mirrorEvents = MirrorModeObserver.New("for MirrorServer");
            _mirrorEvents.ModeChanged += OnModeChanged;
            NetworkServer.RegisterHandler<MirrorMessage>(OnReceivedMessage, false);
        }

        public override void Dispose()
        {
            _mirrorEvents.ModeChanged -= OnModeChanged;
            NetworkServer.UnregisterHandler<MirrorMessage>();
            base.Dispose();
        }

        protected override void SendToClient(int clientId, byte[] data, bool reliable)
        {
            var conn = GetConnectionToClient(clientId);
            if (conn == null) return;
            var message = new MirrorMessage { data = data };
            conn.Send(message, reliable ? Channels.Reliable : Channels.Unreliable);
        }

        protected override void OnAfterClientConnected(int clientId)
        {
            // Be extra cautious — handlers can drop across mode/state transitions.
            NetworkServer.ReplaceHandler<MirrorMessage>(OnReceivedMessage, false);

            foreach (var peer in ClientIDs)
            {
                if (peer == clientId)
                {
                    // Tell the newly connected client its own id and the existing peer list.
                    var otherPeerIDs = ClientIDs.Where(x => x != clientId).ToArray();
                    var packet = new BytesWriter()
                        .WriteString(MirrorMessageTags.PEER_INIT)
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
                        .WriteString(MirrorMessageTags.PEER_JOINED)
                        .WriteInt(clientId);
                    Debug.unityLogger.Log(LogType.Log, Tag,
                        $"Notified client {peer} about new client {clientId}");
                    SendToClient(peer, packet.Bytes, reliable: true);
                }
            }
        }

        protected override void OnAfterClientDisconnected(int clientId)
        {
            NetworkServer.ReplaceHandler<MirrorMessage>(OnReceivedMessage, false);

            foreach (var peerId in ClientIDs)
            {
                var packet = new BytesWriter()
                    .WriteString(MirrorMessageTags.PEER_LEFT)
                    .WriteInt(clientId);
                Debug.unityLogger.Log(LogType.Log, Tag,
                    $"Notified client {peerId} about {clientId} leaving");
                SendToClient(peerId, packet.Bytes, reliable: true);
            }
        }

        private void OnServerStartedInternal()
        {
#if MIRROR_89_OR_NEWER
            NetworkManager.singleton.transport.OnServerConnectedWithAddress += OnServerConnected;
#else
            NetworkManager.singleton.transport.OnServerConnected += OnServerConnected;
#endif
            NetworkManager.singleton.transport.OnServerDisconnected += OnServerDisconnected;
            RaiseServerStarted();
        }

        private void OnServerShutdownInternal()
        {
#if MIRROR_89_OR_NEWER
            NetworkManager.singleton.transport.OnServerConnectedWithAddress -= OnServerConnected;
#else
            NetworkManager.singleton.transport.OnServerConnected -= OnServerConnected;
#endif
            NetworkManager.singleton.transport.OnServerDisconnected -= OnServerDisconnected;
            RaiseServerStopped();
        }

        private void OnModeChanged(NetworkManagerMode oldMode, NetworkManagerMode newMode)
        {
            NetworkServer.ReplaceHandler<MirrorMessage>(OnReceivedMessage, false);

            // In Host mode the server and the internal client both start; the internal client
            // connects immediately. The host client has id 0, so synthesize the connection event.
            if (newMode == NetworkManagerMode.Host)
            {
                OnServerStartedInternal();
                OnServerConnected(0, "localhost");
            }
            else if (newMode == NetworkManagerMode.ServerOnly)
            {
                if (oldMode == NetworkManagerMode.Host)
                    OnServerDisconnected(0); // Host → ServerOnly: internal client disconnects
                else if (oldMode == NetworkManagerMode.Offline)
                    OnServerStartedInternal();
            }
            else if (newMode == NetworkManagerMode.Offline &&
                     (oldMode == NetworkManagerMode.ServerOnly || oldMode == NetworkManagerMode.Host))
            {
                if (oldMode == NetworkManagerMode.Host)
                    OnServerDisconnected(0);
                OnServerShutdownInternal();
            }
        }

#if MIRROR_89_OR_NEWER
        private void OnServerConnected(int connId, string addr) => HandleClientConnected(connId);
#else
        private void OnServerConnected(int connId) => HandleClientConnected(connId);
#endif

        private void OnServerDisconnected(int connId) => HandleClientDisconnected(connId);

        private void OnReceivedMessage(NetworkConnectionToClient connection, MirrorMessage message) =>
            HandleIncomingPayload(connection.connectionId, message.data);

        private async void SendToClientDelayed(int clientId, byte[] data, bool reliable, int delayMS)
        {
            await Task.Delay(delayMS);
            SendToClient(clientId, data, reliable);
        }

        private static NetworkConnectionToClient GetConnectionToClient(int connId)
        {
            foreach (var conn in NetworkServer.connections)
                if (conn.Key == connId)
                    return conn.Value;
            return null;
        }
    }
}
#endif
