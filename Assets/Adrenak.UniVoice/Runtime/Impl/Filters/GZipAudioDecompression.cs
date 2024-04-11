namespace Adrenak.UniVoice.Filters {
    public class GZipAudioDecompression : IAudioFilter {
        public byte[] Run(byte[] input) {
            if (input == null || input.Length == 0)
                return new byte[0];
            return Utils.GZip.Decompress(input);
        }
    }
}
