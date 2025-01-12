using System;

using UnityEngine;

namespace Adrenak.UniVoice.Filters {
    /// <summary>
    /// A filter that applies Gaussian blur over audio data to smoothen it.
    /// This is somewhat effective in removing noise from the audio.
    /// </summary>
    public class GaussianAudioBlur : IAudioFilter {
        readonly float sigma;
        readonly int range;
        byte[] lastInput;

        public GaussianAudioBlur(float sigma = 2, int range = 2) {
            this.sigma = sigma;
            this.range = range;
        }

        public AudioFrame Run(AudioFrame frame) {
            var input = frame.samples;
            if (input == null || input.Length == 0) {
                frame.samples = null;
                return frame;
            }

            // If this is the first audio input we've received, we simply apply the gaussian filter
            // and return the result.
            if (lastInput == null) {
                lastInput = input;
                frame.samples = Utils.Bytes.FloatsToBytes(
                    ApplyGaussianFilter(Utils.Bytes.BytesToFloats(input))
                );
                return frame;
            }

            // Else, if we've had some input before, we also consider the previously processed 
            // audio. We make an array that has both the previous and the current input, smoothen 
            // it, and then return the second half of the array. This reducing jittering by making
            // the smoothing a little more seamless.
            else {
                // Create an all input, that also has the input from the last time this filter ran.
                byte[] allInput = new byte[lastInput.Length + input.Length];
                Buffer.BlockCopy(lastInput, 0, allInput, 0, lastInput.Length);
                Buffer.BlockCopy(input, 0, allInput, lastInput.Length, input.Length);

                // smoothen all input
                byte[] allInputSmooth = Utils.Bytes.FloatsToBytes(
                    ApplyGaussianFilter(Utils.Bytes.BytesToFloats(allInput))
                );

                // get the second half of the smoothened values
                byte[] result = new byte[input.Length];
                Buffer.BlockCopy(allInputSmooth, lastInput.Length, result, 0, input.Length);

                lastInput = input;
                frame.samples = result;
                return frame;
            }
        }

        float[] ApplyGaussianFilter(float[] inputArray) {
            int length = inputArray.Length;
            float[] smoothedArray = new float[length];

            for (int i = 0; i < length; i++) {
                float sum = 0.0f;
                float weightSum = 0.0f;

                for (int j = -range; j <= range; j++) {
                    int index = i + j;
                    if (index >= 0 && index < length) {
                        float weight = Gaussian(j, sigma);
                        sum += inputArray[index] * weight;
                        weightSum += weight;
                    }
                }

                smoothedArray[i] = sum / weightSum;
            }

            return smoothedArray;
        }

        float Gaussian(int x, float sigma) {
            return (float)Mathf.Exp(-(x * x) / (2 * sigma * sigma))
            / ((float)Mathf.Sqrt(2 * Mathf.PI) * sigma);
        }
    }
}
