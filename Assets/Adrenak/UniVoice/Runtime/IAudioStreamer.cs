using System;

namespace Adrenak.UniVoice {
    public interface IAudioStreamer : IDisposable {
        void Stream(int index, float[] audioSamples);
    }
}
