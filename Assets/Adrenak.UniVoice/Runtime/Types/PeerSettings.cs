namespace Adrenak.UniVoice {
    [System.Serializable]
    /// <summary>
    /// Represents settings associated with a peer in the chatroom
    /// </summary>
    public class PeerSettings {
        /// <summary>
        /// Whether this peer is muted. Use this to stop incoming audio from a peer.
        /// </summary>
        public bool muteThem = false;

        /// <summary>
        /// Whether this peer will receive our voice. Use this to 
        /// stop sending your audio to a peer.
        /// </summary>
        public bool muteSelf = false;
    }
}