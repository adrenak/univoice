using System;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Responsible for playing audio that is sent to it. You'd normally want a
    /// <see cref="UnityEngine.AudioSource"/> based implementation to play the audio.
    /// </summary>
    public interface IAudioOutput : IDisposable {
        /// <summary>
        /// Feeds the data to the output implementation 
        /// </summary>
        /// <param name="segmentIndex">The index of the segment of samples from the audio</param>
        /// <param name="frequency">The frequency/sampling rate of the audio</param>
        /// <param name="channelCount">The number of channels in the audio</param>
        /// <param name="audioSamples">The number of samples being fed</param>
        void Feed(int segmentIndex, int frequency, int channelCount, float[] audioSamples);
    }
}
