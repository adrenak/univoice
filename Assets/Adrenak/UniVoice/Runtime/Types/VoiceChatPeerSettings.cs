namespace Adrenak.UniVoice {
    /// <summary>
    /// Represents settings associated with a peer in the chatroom
    /// </summary>
    public class VoiceChatPeerSettings {
        /// <summary>
        /// Whether this peer is muted. Use this to ignore a person.
        /// </summary>
        public bool muteIncoming = false;

        /// <summary>
        /// Whether this peer will receive out voice. Use this to keep
        /// say something without a person hearing.
        /// </summary>
        public bool muteOutgoing = false;
    }
}