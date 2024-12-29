using System;

using Adrenak.UniMic;

using UnityEngine;

namespace Adrenak.UniVoice.Inputs {
    /// <summary>
    /// An <see cref="IAudioInput"/> implementation based on UniMic.
    /// For more on UniMic, visit https://www.github.com/adrenak/unimic
    /// </summary>
    public class UniMicInput : IAudioInput {
        const string TAG = "UniMicInput";

        public event Action<AudioFrame> OnFrameReady;

        public Mic.Device Device { get; private set; }

        public UniMicInput(Mic.Device device) {
            Device = device;
            device.OnFrameCollected += OnFrameCollected;
        }

        private void OnFrameCollected(int frequency, int channels, float[] samples) {
            var frame = new AudioFrame {
                timestamp = 0,
                frequency = frequency,
                channelCount = channels,
                samples = Utils.Bytes.FloatsToBytes(samples)
            };
            OnFrameReady?.Invoke(frame);
        }

        public void Dispose() {
            Device.OnFrameCollected -= OnFrameCollected;
            Debug.unityLogger.Log(TAG, "Disposed");
        }
    }
}