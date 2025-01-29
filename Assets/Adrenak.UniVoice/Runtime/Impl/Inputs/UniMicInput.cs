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

        public Mic.Device device;
        public Mic.Device Device { 
            get => device;
            set {
                if (device == value)
                    return;
                if(device != null)
                    device.OnFrameCollected -= OnFrameCollected;
                device = value;
                if(device != null)
                    device.OnFrameCollected += OnFrameCollected;
            } 
        }

        public UniMicInput(Mic.Device device) {
            Device = device;
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
            if(Device != null)
                Device.OnFrameCollected -= OnFrameCollected;
            Debug.unityLogger.Log(TAG, "Disposed");
        }
    }
}