using System;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Source of user voice input. This would usually be implemented over
    /// a microphone. 
    /// </summary>
    public interface IAudioInput : IDisposable {
        /// <summary>
        /// Fired when a segment (sequence of audio samples) is ready
        /// </summary>
        event Action<int, float[]> OnSegmentReady;

        /// <summary>
        /// The sampling frequency of the audio
        /// </summary>
        int Frequency { get; }

        /// <summary>
        /// The number of channels in the audio
        /// </summary>
        int ChannelCount { get; }

        /// <summary>
        /// The number of segments (a segment is a sequence of audio samples)
        /// that are emitted from the source every second.
        /// A 16000 Hz source with a rate of 10 will output a segment with
        /// 1600 samples every 100 milliseconds.
        /// </summary>
        int SegmentRate { get; }
    }

    public static class IAudioInputExtensions {
        public static int GetSegmentLength(this IAudioInput input) {
            return input.Frequency * input.ChannelCount / input.SegmentRate;
        }
    }
}
