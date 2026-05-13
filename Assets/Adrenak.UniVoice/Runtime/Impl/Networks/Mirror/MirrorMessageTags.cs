#if MIRROR
namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// Mirror-specific protocol tags. Because Mirror does not expose remote-client
    /// lifecycle events to peers natively, <see cref="MirrorServer"/> drives these
    /// extra notifications and <see cref="MirrorClient"/> consumes them in addition
    /// to the shared <see cref="AudioBroadcastTags"/>.
    /// </summary>
    public static class MirrorMessageTags
    {
        public const string PEER_INIT = "PEER_INIT";
        public const string PEER_JOINED = "PEER_JOINED";
        public const string PEER_LEFT = "PEER_LEFT";
    }
}
#endif
