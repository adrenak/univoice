using UnityEngine;
using UnityEngine.UI;

namespace Adrenak.UniVoice.Samples {
    public class FishnetTagsSample : MonoBehaviour {
        const string TAG = "[FishnetTagsSample]";

        [SerializeField] Toggle myTagRed;
        [SerializeField] Toggle myTagBlue;
        [SerializeField] Toggle mutedTagRed;
        [SerializeField] Toggle mutedTagBlue;
        [SerializeField] Toggle deafenedTagRed;
        [SerializeField] Toggle deafenedTagBlue;

        void Awake() {
            myTagRed.onValueChanged.AddListener(x =>
                UniVoiceFishNetSetupSample.ClientSession.Client.UpdateVoiceSettings(s => s.SetMyTag("red", x)));
            myTagBlue.onValueChanged.AddListener(x =>
                UniVoiceFishNetSetupSample.ClientSession.Client.UpdateVoiceSettings(s => s.SetMyTag("blue", x)));

            mutedTagRed.onValueChanged.AddListener(x =>
                UniVoiceFishNetSetupSample.ClientSession.Client.UpdateVoiceSettings(s => s.SetMutedTag("red", x)));
            mutedTagBlue.onValueChanged.AddListener(x =>
                UniVoiceFishNetSetupSample.ClientSession.Client.UpdateVoiceSettings(s => s.SetMutedTag("blue", x)));

            deafenedTagRed.onValueChanged.AddListener(x =>
                UniVoiceFishNetSetupSample.ClientSession.Client.UpdateVoiceSettings(s => s.SetDeafenedTag("red", x)));
            deafenedTagBlue.onValueChanged.AddListener(x =>
                UniVoiceFishNetSetupSample.ClientSession.Client.UpdateVoiceSettings(s => s.SetDeafenedTag("blue", x)));
        }
    }
}
