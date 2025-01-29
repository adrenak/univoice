using Concentus;
using Concentus.Enums;
using System;

/*
 * Opus encoding and decoding are VERY important for any real world use of UniVoice as without
 * encoding the size of audio data is much (over 10x) larger.
 * For more info see https://www.github.com/adrenak/concentus-unity
 */
namespace Adrenak.UniVoice.Filters {
    /// <summary>
    /// A filter that encodes audio using Opus. Use this as an output filter
    /// to reduce the size of outgoing client audio
    /// </summary>
    public class ConcentusEncodeFilter : IAudioFilter {
        public ConcentusFrequencies SamplingFrequency { get; private set; }
        IOpusEncoder encoder;
        IResampler resampler;
        byte[] encodeBuffer;
        float[] resampleBuffer;
        int inputDuration;
        int inputChannels;
        int inputFrequency;
        int resamplerChannelCount;
        float[] bytesToFloats;
        byte[] floatsToBytes;
        int resamplerQuality;
        int encoderComplexity;
        int encoderBitrate;

        /// <summary>
        /// Creates a Concentus encode filter
        /// </summary>
        /// <param name="encodeFrequency">
        /// The frequency the encoder runs at.
        /// If the input audio frequency is different from this value, it will be resampled before encode.
        /// </param>
        /// <param name="resamplerQuality">Resampler quality [1, 10]</param>
        /// <param name="encoderComplexity">Encoder complexity [1, 10]</param>
        /// <param name="encoderBitrate">Encoder bitrate [16000, 256000]. Set to -1 to enable variable bitrate.</param>
        /// <param name="encodeBufferLength">
        /// The length of the encode buffer. Default is 46080 to fit a large sample
        /// with frequency 48000, duration 120ms and 2 channels. This should be enough
        /// for almost all scenarios. Increase if you need more.
        /// </param>
        public ConcentusEncodeFilter(
            ConcentusFrequencies encodeFrequency = ConcentusFrequencies.Frequency_16000,
            int resamplerQuality = 2,
            int encoderComplexity = 3,
            int encoderBitrate = 64000,
            int encodeBufferLength = 46080
        ) {
            SamplingFrequency = encodeFrequency;
            this.resamplerQuality = Math.Clamp(resamplerQuality, 1, 10);
            this.encoderComplexity = Math.Clamp(encoderComplexity, 1, 10);
            this.encoderBitrate = Math.Clamp(encoderBitrate, 16000, 256000);
            encodeBuffer = new byte[encodeBufferLength];
        }

        public AudioFrame Run(AudioFrame input) {
            inputChannels = input.channelCount;
            inputFrequency = input.frequency;
            inputDuration = ((input.samples.Length / 4) * 1000) / (input.frequency * input.channelCount);

            CreateNewResamplerAndEncoderIfNeeded();

            Span<float> toEncode;
            bytesToFloats = Utils.Bytes.BytesToFloats(input.samples);
            toEncode = bytesToFloats;

            if (inputFrequency != (int)SamplingFrequency) 
                toEncode = Resample(bytesToFloats);

            var encodeResult = Encode(toEncode, out Span<byte> encoded);
            if (encodeResult > 0) {
                floatsToBytes = encoded.ToArray();
                return new AudioFrame {
                    timestamp = input.timestamp,
                    channelCount = inputChannels,
                    samples = floatsToBytes,
                    frequency = (int)SamplingFrequency
                };
            }
            else {
                return new AudioFrame {
                    timestamp = input.timestamp,
                    channelCount = inputChannels,
                    samples = new byte[0],
                    frequency = (int)SamplingFrequency
                };
            }
        }

        void CreateNewResamplerAndEncoderIfNeeded() {
            if (resampleBuffer == null || resampleBuffer.Length != (int)SamplingFrequency * inputDuration * inputChannels / 1000)
                resampleBuffer = new float[(int)SamplingFrequency * inputDuration * inputChannels / 1000];

            if (resampler == null) {
                resamplerChannelCount = inputChannels;
                resampler = ResamplerFactory.CreateResampler(inputChannels, inputFrequency, (int)SamplingFrequency, resamplerQuality);
            }
            else {
                resampler.GetRates(out int in_rate, out int out_rate);
                if (in_rate != inputFrequency || out_rate != (int)SamplingFrequency || resamplerChannelCount != inputChannels) {
                    resampler.Dispose();
                    resamplerChannelCount = inputChannels;
                    resampler = ResamplerFactory.CreateResampler(inputChannels, inputFrequency, (int)SamplingFrequency, resamplerQuality);
                }
            }

            if (encoder == null || encoder.SampleRate != (int)SamplingFrequency || encoder.NumChannels != inputChannels) {
                encoder?.Dispose();
                encoder = OpusCodecFactory.CreateEncoder((int)SamplingFrequency, inputChannels, OpusApplication.OPUS_APPLICATION_VOIP);
                encoder.Complexity = encoderComplexity;
                if(encoderBitrate == -1) 
                    encoder.UseVBR = true;
                else {
                    encoder.UseVBR = false;
                    encoder.Bitrate = encoderBitrate;
                }
            }
        }

        Span<float> Resample(Span<float> samples) {
            // Calculate input and output lengths
            int in_len = samples.Length / inputChannels; // Input samples per channel
            int out_len = (int)SamplingFrequency * inputDuration / 1000; // Output samples per channel

            // Perform resampling into preallocated buffer
            resampler.ProcessInterleaved(samples, ref in_len, resampleBuffer, ref out_len);

            // Return only the valid portion of resampled data
            return resampleBuffer.AsSpan(0, out_len * inputChannels); // Trim to valid samples
        }

        int Encode(Span<float> toEncode, out Span<byte> encoded) {
            int frameSize = (int)SamplingFrequency * inputDuration / 1000; // Samples per channel
            int totalSamples = frameSize * inputChannels; // Total interleaved samples

            if (toEncode.Length < totalSamples) {
                encoded = Span<byte>.Empty;
                return 0;
            }

            // Use preallocated encodeBuffer
            int result = encoder.Encode(toEncode.Slice(0, totalSamples), frameSize, encodeBuffer, encodeBuffer.Length);

            if (result > 0) 
                encoded = encodeBuffer.AsSpan(0, result); // Trim to actual encoded size
            else 
                encoded = Span<byte>.Empty;

            return result; // Return number of bytes written or 0 on failure
        }
    }
}