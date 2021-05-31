using System;

namespace Adrenak.UniVoice {
    public interface IAudioOutput : IDisposable {
        void Stream(int index, float[] audioSamples);
    }
}
