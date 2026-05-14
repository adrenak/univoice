#if MIRROR
using System.Linq;
using Mirror;
using Adrenak.BRW;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// Mirror implementation of <see cref="IAudioClient{T}"/>.
    /// Mirror's transport does not expose remote-client lifecycle events to other
    /// clients, so the protocol is augmented with the PEER_INIT / PEER_JOINED /
    /// PEER_LEFT tags driven by <see cref="MirrorServer"/>. Everything else is
    /// inherited from <see cref="AudioClientBase{TPlayerId}"/>.
    /// </summary>
    public class MirrorClient : AudioClientBase<int>
    {
        protected sealed override string Tag => "[MirrorClient]";
        protected override bool HasLocalId => ID != -1;
        protected override bool IsConnected => NetworkClient.active;

        private readonly MirrorModeObserver _mirrorEvents;

        public MirrorClient()
        {
            ID = -1;
            _mirrorEvents = MirrorModeObserver.New("for MirrorClient");
            _mirrorEvents.ModeChanged += OnModeChanged;
            NetworkClient.RegisterHandler<MirrorMessage>(OnReceivedMessage, false);
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void ResetLocalId() => ID = -1;

        protected override void SendToServer(byte[] data, bool reliable)
        {
            var message = new MirrorMessage { data = data };
            NetworkClient.Send(message, reliable ? Channels.Reliable : Channels.Unreliable);
        }

        protected override void WriteId(BytesWriter writer, int id) => writer.WriteInt(id);
        protected override int ReadId(BytesReader reader) => reader.ReadInt();

        protected override void DispatchIncomingTag(string tag, BytesReader reader)
        {
            switch (tag)
            {
                case MirrorMessageTags.PEER_INIT:
                {
                    var localId = reader.ReadInt();
                    var existingPeers = reader.ReadIntArray();
                    HandleLocalJoined(localId, existingPeers);
                    break;
                }
                case MirrorMessageTags.PEER_JOINED:
                    HandlePeerJoined(reader.ReadInt());
                    break;
                case MirrorMessageTags.PEER_LEFT:
                    HandlePeerLeft(reader.ReadInt());
                    break;
                default:
                    base.DispatchIncomingTag(tag, reader);
                    break;
            }
        }

        private void OnModeChanged(NetworkManagerMode oldMode, NetworkManagerMode newMode)
        {
            // Handlers can be flaky across mode transitions; re-register to be safe.
            NetworkClient.ReplaceHandler<MirrorMessage>(OnReceivedMessage);

            var clientOnlyToOffline = newMode == NetworkManagerMode.Offline && oldMode == NetworkManagerMode.ClientOnly;
            var hostToServerOnlyOrOffline = oldMode == NetworkManagerMode.Host;

            if (clientOnlyToOffline || hostToServerOnlyOrOffline)
            {
                // Only unregister when this device was actually a client. A Host going to ServerOnly
                // still needs the handler because MirrorServer uses it server-side.
                if (clientOnlyToOffline)
                    NetworkClient.UnregisterHandler<MirrorMessage>();

                HandleLocalLeft();
            }
        }

        private void OnReceivedMessage(MirrorMessage msg) => HandleIncomingPayload(msg.data);
    }
}
#endif
