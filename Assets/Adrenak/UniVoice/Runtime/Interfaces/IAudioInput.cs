using System;

namespace Adrenak.UniVoice {
    public interface IAudioInput {
        event Action<int, float[]> OnSegmentReady;
        int ChannelCount { get; }

    }
}
