namespace Adrenak.UniVoice {
    /// <summary>
    /// An abstract factory that creates and 
    /// destroys <see cref="IAudioOutput"/> instances.
    /// </summary>
    public interface IAudioOutputFactory {
        /// <summary>
        /// Creates an instnace of a concrete <see cref="IAudioOutput"/> class
        /// </summary>
        /// <param name="peerID">The ID of the peer for which </param>
        /// <param name="frequency">Frequency/sample rate of the audio </param>
        /// <param name="channelCount">Number of audio channels in data</param>
        /// <param name="samplesLen">Number of samples in audio data</param>
        IAudioOutput Create(int frequency, int channelCount, int samplesLen);

        /// <summary>
        /// Destroys the given <see cref="IAudioOutput"/>
        /// </summary>
        /// <param name="audioOutput"></param>
        void Destroy(IAudioOutput audioOutput);
    }
}