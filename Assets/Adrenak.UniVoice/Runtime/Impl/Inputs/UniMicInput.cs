#if UNIVOICE_UNIMIC_INPUT
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

        public event Action<long, byte[]> OnSamplesReady;

        public int Frequency => Mic.Instance.Frequency;

        public int ChannelCount =>
            Mic.Instance.AudioClip == null ? 0 : Mic.Instance.AudioClip.channels;

        public int SegmentRate => 1000 / Mic.Instance.SampleDurationMS;

        public UniMicInput(int deviceIndex = 0, int frequency = 16000, int sampleLen = 100) {
            if (Mic.Instance.Devices.Count == 0)
                throw new Exception("Must have recording devices for Microphone input");

            Mic.Instance.SetDeviceIndex(deviceIndex);
            Mic.Instance.StartRecording(frequency, sampleLen);
            Debug.unityLogger.Log(TAG, "Start recording.");
            Mic.Instance.OnTimestampedSampleReady += Mic_OnTimestampedSampleReady;
        }

        void Mic_OnTimestampedSampleReady(long timestamp, float[] samples) {
             OnSamplesReady?.Invoke(timestamp, Utils.Bytes.FloatsToBytes(samples));
        }

        public void Dispose() {
            Mic.Instance.OnTimestampedSampleReady -= Mic_OnTimestampedSampleReady;
        }
    }
}
#endif