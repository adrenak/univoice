using System;
using UnityEngine;

using Adrenak.UniMic;

namespace Adrenak.UniVoice {
    public class UniMicAudioInput : Mic, IAudioInput {
        public event Action<int, float[]> OnSegmentReady;

        new int Frequency => Frequency;

        public int ChannelCount => AudioClip == null ? 0 : AudioClip.channels;

        public int SegmentRate => 1000 / SampleDurationMS;

        UniMicAudioInput() { }

        public static UniMicAudioInput New() {
            var go = new GameObject("UniMicAudioInput");
            DontDestroyOnLoad(go);
            var cted = go.AddComponent<UniMicAudioInput>();
            cted.OnSampleReady += (index, samples) =>
                cted.OnSegmentReady?.Invoke(index, samples);

            return cted;
        }
    }
}
