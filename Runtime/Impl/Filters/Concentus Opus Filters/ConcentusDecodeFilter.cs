using Concentus;
using System;

/*
 * Opus encoding and decoding are VERY important for any real world use of UniVoice as without
 * encoding the size of audio data is much (over 10x) larger.
 * For more info see https://www.github.com/adrenak/concentus-unity
 */
namespace Adrenak.UniVoice.Filters {
    /// <summary>
    /// Decodes Opus encoded audio. Use this as a filter for incoming client audio.
    /// </summary>
    public class ConcentusDecodeFilter : IAudioFilter {
        IOpusDecoder decoder;
        float[] decodeBuffer;
        int inputChannelCount;
        int inputFrequency;
        byte[] floatsToBytes;

        /// <summary>
        /// Creates a Concentus decode filter.
        /// </summary>
        /// <param name="decodeBufferLength">
        /// The length of the decode buffer. Default is 11520 to fit a large sample
        /// with frequency 48000, duration 120ms and 2 channels. This should be enough
        /// for almost all scenarios. Increase if you need more.
        /// </param>
        public ConcentusDecodeFilter(int decodeBufferLength = 11520) {
            decodeBuffer = new float[decodeBufferLength];
        }

        public AudioFrame Run(AudioFrame input) {
            inputChannelCount = input.channelCount;
            inputFrequency = input.frequency;

            CreateNewDecoderIfNeeded();

            var decodeResult = Decode(input.samples, out Span<float> decoded);
            if (decodeResult > 0) {
                floatsToBytes = Utils.Bytes.FloatsToBytes(decoded.ToArray());
                return new AudioFrame {
                    timestamp = input.timestamp,
                    samples = floatsToBytes,
                    channelCount = inputChannelCount,
                    frequency = inputFrequency
                };
            }
            else {
                return new AudioFrame {
                    timestamp = input.timestamp,
                    samples = new byte[0],
                    channelCount = inputChannelCount,
                    frequency = inputFrequency
                };
            }
        }

        int Decode(Span<byte> toDecode, out Span<float> decoded) {
            // Decode the Opus packet into preallocated buffer
            int samplesPerChannel = decoder.Decode(toDecode, decodeBuffer, decodeBuffer.Length);

            if (samplesPerChannel > 0) {
                int totalSamples = samplesPerChannel * inputChannelCount; // Total samples across all channels
                decoded = decodeBuffer.AsSpan(0, totalSamples); // Trim to valid samples
            }
            else {
                decoded = Span<float>.Empty;
            }

            return samplesPerChannel; // Return number of samples per channel or 0 on failure
        }

        void CreateNewDecoderIfNeeded() {
            if (decoder == null || decoder.SampleRate != inputFrequency || decoder.NumChannels != inputChannelCount) {
                decoder?.Dispose();
                decoder = OpusCodecFactory.CreateDecoder(inputFrequency, inputChannelCount);
            }
        }
    }
}