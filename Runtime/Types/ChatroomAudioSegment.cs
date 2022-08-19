namespace Adrenak.UniVoice {
    /// <summary>
    /// A data structure representing the audio transmitted over the network.
    /// </summary>
    public struct ChatroomAudioSegment {
        /// <summary>
        /// ID of the peer that has sent the data
        /// </summary>
        public short id;

        /// <summary>
        /// The segment index of the audio samples
        /// </summary>
        public int segmentIndex;

        /// <summary>
        /// The frequency (or sampling rate) of the audio
        /// </summary>
        public int frequency;

        /// <summary>
        /// THe number of channels in the audio
        /// </summary>
        public int channelCount;

        /// <summary>
        /// A float array representing the audio sample data
        /// </summary>
        public float[] samples;
    }
}