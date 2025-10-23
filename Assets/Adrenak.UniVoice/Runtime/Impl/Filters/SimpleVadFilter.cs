namespace Adrenak.UniVoice.Filters {
    public class SimpleVadFilter : IAudioFilter {
        private readonly SimpleVad _vad;

        public SimpleVadFilter(SimpleVad vad) {
            _vad = vad;
        }

        public AudioFrame Run(AudioFrame input) {
            _vad.Process(input.frequency, input.channelCount, Utils.Bytes.BytesToFloats(input.samples));
            if (_vad.IsSpeaking) {
                return input;
            }
            return default;
        }
    }
}
