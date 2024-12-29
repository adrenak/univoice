using Adrenak.UnityOpus;

using System;

/*
 * Opus encoding and decoding are VERY important for any real world use of UniVoice as without
 * encoding the size of audio data is much (over 10x) larger.
 * For more info see https://www.github.com/adrenak/UnityOpus
 */
namespace Adrenak.UniVoice {
    /// <summary>
    /// A filter that encodes audio using Opus. Use this as an output filter
    /// to reduce the size of outgoing client audio
    /// </summary>
    public class OpusEncodeFilter : IAudioFilter {
        Encoder encoder;
        byte[] outputBuffer;

        public OpusEncodeFilter(Encoder encoder) {
            this.encoder = encoder;
        }

        public AudioFrame Run(AudioFrame input) {
            if(outputBuffer == null)
                outputBuffer = new byte[input.samples.Length * 4];
            else if(input.samples.Length != outputBuffer.Length * 4)
                outputBuffer = new byte[input.samples.Length * 4];

            int encodeResult = encoder.Encode(Utils.Bytes.BytesToFloats(input.samples), outputBuffer);
            if (encodeResult > 0) {
                byte[] encodedBytes = new byte[encodeResult];
                Array.Copy(outputBuffer, encodedBytes, encodedBytes.Length);
                return new AudioFrame {
                    timestamp = 0,
                    frequency = input.frequency,
                    channelCount = input.channelCount,
                    samples = encodedBytes
                };
            }
            else {
                return new AudioFrame {
                    timestamp = 0,
                    frequency = input.frequency,
                    channelCount = input.channelCount,
                    samples = null
                };
            }
        }
    }

    /// <summary>
    /// Decodes Opus encoded audio. Use this as a filter for incoming client audio.
    /// </summary>
    public class OpusDecodeFilter : IAudioFilter {
        Decoder decoder;
        float[] outputBuffer;

        public OpusDecodeFilter(Decoder decoder, int outputBufferLength = 48000) {
            this.decoder = decoder;
            outputBuffer = new float[outputBufferLength];
        }

        public AudioFrame Run(AudioFrame input) {
            var decodeResult = decoder.Decode(input.samples, input.samples.Length, outputBuffer);
            if(decodeResult > 0) {
                float[] decoded = new float[decodeResult];
                Array.Copy(outputBuffer, decoded, decoded.Length);
                return new AudioFrame {
                    timestamp = 0,
                    frequency = input.frequency,
                    channelCount = input.channelCount,
                    samples = Utils.Bytes.FloatsToBytes(decoded)
                };
            }
            else {
                return new AudioFrame {
                    timestamp = 0,
                    frequency = input.frequency,
                    channelCount = input.channelCount,
                    samples = null
                };
            }
        }
    }
}