#if UNITY_NETCODE_GAMEOBJECTS
namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// NGO-specific protocol tags. Because NGO does not expose remote-client
    /// lifecycle events to peers natively, <see cref="NGOServer"/> drives these
    /// extra notifications and <see cref="NGOClient"/> consumes them in addition
    /// to the shared <see cref="AudioBroadcastTags"/>.
    /// </summary>
    public static class NGOMessageTags
    {
        /// <summary>The NGO named-message channel UniVoice messages flow through.</summary>
        public const string MESSAGE_NAME = "UniVoice_NGO";

        public const string PEER_INIT = "PEER_INIT";
        public const string PEER_JOINED = "PEER_JOINED";
        public const string PEER_LEFT = "PEER_LEFT";
    }
}
#endif
