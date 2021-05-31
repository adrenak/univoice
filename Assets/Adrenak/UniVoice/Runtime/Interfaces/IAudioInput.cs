using System;

namespace Adrenak.UniVoice {
    public interface IAudioInput {
        event Action<int, float[]> OnSegmentReady;

        int Frequency { get; }
        int ChannelCount { get; }
        int SegmentRate { get; }
    }
}
