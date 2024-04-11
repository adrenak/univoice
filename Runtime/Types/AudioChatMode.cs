namespace Adrenak.UniVoice {
    [System.Serializable]
    /// <summary>
    /// Represents the mode that a <see cref="AudioChat"/> is in.
    /// </summary>
    public enum AudioChatMode {
        /// <summary>
        /// Neither connected to a chatroom nor hosting one.
        /// </summary>
        Idle,

        /// <summary>
        /// We're only relaying audio data, not participating in audio chat. Use for dedicated
        /// servers based networking.
        /// </summary>
        Server,

        /// <summary>
        /// Hosting audio chat and also participating in the chat. Use this for ServerClient-Client
        /// and peer-to-peer based networking.
        /// </summary>
        Host,

        /// <summary>
        /// Currently connected to an audio chat session created by someone else (Server or Host)
        /// </summary>
        Guest
    }
}