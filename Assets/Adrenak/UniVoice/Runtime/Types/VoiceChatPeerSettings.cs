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
        /// Whether this peer will receive out voice. Use this to 
        /// stop sending your audio to a peer.
        /// </summary>
        public bool muteOutgoing = false;
    }
}