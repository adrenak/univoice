using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace Adrenak.UniVoice.Samples {
    public static class ToggleExtension{
        public static void SetIsOnWithoutNotify(this Toggle instance, bool value) {
            var originalEvent = instance.onValueChanged;
            instance.onValueChanged = new Toggle.ToggleEvent();
            instance.isOn = value;
            instance.onValueChanged = originalEvent;
        }
    }

    public class PeerView : MonoBehaviour {
        public event Action<bool> OnAllowIncomingAudioChange;
        public event Action<bool> OnAllowOutgoingAudioChange;

        [SerializeField] Text idText;
        [SerializeField] Transform barContainer;
        [SerializeField] Transform barTemplate;
        [SerializeField] Toggle speakerToggle;
        [SerializeField] Toggle micToggle;

        public bool AllowIncomingAudio {
            get => speakerToggle.isOn;
            set => speakerToggle.SetIsOnWithoutNotify(value);
        }

        public bool AllowOutgoingAudio {
            get => micToggle.isOn;
            set => micToggle.SetIsOnWithoutNotify(value);
        }

        List<Transform> bars = new List<Transform>();

        void Start() {
            speakerToggle.onValueChanged.AddListener(value =>
                OnAllowIncomingAudioChange?.Invoke(value));

            micToggle.onValueChanged.AddListener(value =>
                OnAllowOutgoingAudioChange?.Invoke(value));
        }

        public void SetPeerID(int id) {
            idText.text = id.ToString();
        }

        public void DisplaySpectrum(float[] spectrum) {
            InitBars(spectrum.Length);

            if (spectrum.Length != bars.Count) return;

            for (int i = 0; i < bars.Count; i++)
                bars[i].localScale = new Vector3(1, Mathf.Clamp01(spectrum[i]), 1);
        }

        void InitBars(int count) {
            if (bars.Count == count) return;

            foreach (var bar in bars)
                Destroy(bar.gameObject);
            bars.Clear();

            for (int i = 0; i < count; i++) {
                var instance = Instantiate(barTemplate, barContainer);
                instance.gameObject.SetActive(true);
                bars.Add(instance);
            }
        }
    }
}
