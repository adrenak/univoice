namespace Adrenak.UniVoice {
    [System.Serializable]
    /// <summary>
    /// A data structure representing the audio transmitted over the network.
    /// </summary>
    public struct AudioFrame {
        /// <summary>
        /// The UTC Unix timestamp (in ms) when the samples were captured
        /// </summary>
        public long timestamp;

        /// <summary>
        /// The frequency (or sampling rate) of the audio
        /// </summary>
        public int frequency;

        /// <summary>
        /// THe number of channels in the audio
        /// </summary>
        public int channelCount;

        /// <summary>
        /// A byte array representing the audio sample data
        /// </summary>
        public byte[] samples;
    }
}