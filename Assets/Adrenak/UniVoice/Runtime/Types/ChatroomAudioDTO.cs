namespace Adrenak.UniVoice{
    /// <summary>
    /// A data structure representing the audio transmitted on the network.
    /// </summary>
    /// <param name="id">ID of the peer associated with the data</param>
    /// <param name="segmentIndex">The index of the segment, as per the peer's audio source</param>
    /// <param name="frequency">The frequency/sampling rate of the audio </param>
    /// <param name="channelCount">The number of channels in the audio</param>
    /// <param name="samples">The data representing the samples of the audio</param>
    public struct ChatroomAudioDTO {
        public short id;
        public int segmentIndex;
        public int frequency;
        public int channelCount;
        public float[] samples;
    }
}