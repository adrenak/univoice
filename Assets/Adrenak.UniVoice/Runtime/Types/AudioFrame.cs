namespace Adrenak.UniVoice {
    [System.Serializable]
    /// <summary>
    /// A data structure representing the audio transmitted over the network.
    /// </summary>
    public struct AudioFrame {
        /// <summary>
        /// The UTC Unix timestamp (in ms) when the samples were captured.
        /// The timestamp is local to the client the audio was captured from.
        /// </summary>
        public long timestamp;

        /// <summary>
        /// The frequency (or sampling rate) of the audio
        /// </summary>
        public int frequency;

        /// <summary>
        /// The number of channels in the audio
        /// </summary>
        public int channelCount;

        /// <summary>
        /// A byte array representing the audio data
        /// </summary>
        public byte[] samples;
    }
}