#if UNIVOICE_FILTER_RNNOISE4UNITY
using System;

using Adrenak.RNNoise4Unity;

namespace Adrenak.UniVoice.Filters {
    public class RNNoiseFilter : IAudioFilter {
        readonly Denoiser denoiser;

        public RNNoiseFilter() {
            denoiser = new Denoiser();
        }

        public AudioFrame Run(AudioFrame input) {
            var data = Utils.Bytes.BytesToFloats(input.samples);

            denoiser.Denoise(data.AsSpan(), false);

            return new AudioFrame {
                timestamp = input.timestamp,
                channelCount = input.channelCount,
                frequency = input.frequency,
                samples = Utils.Bytes.FloatsToBytes(data)
            };
        }
    }
}
#endif