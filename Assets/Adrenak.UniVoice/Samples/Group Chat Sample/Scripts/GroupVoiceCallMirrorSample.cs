#if UNIVOICE_MIRROR_NETWORK
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;

using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniVoice.Networks;
using Adrenak.UniVoice.Filters;

namespace Adrenak.UniVoice.Samples {
    public class GroupVoiceCallMirrorSample : MonoBehaviour {
        public Transform peerViewContainer;
        public PeerView peerViewTemplate;
        public Text chatroomMessage;
        public Toggle muteSelfToggle;
        public Toggle muteOthersToggle;

        AudioChat audioChat;
        Dictionary<int, PeerView> peerViews = new Dictionary<int, PeerView>();

        IEnumerator Start() {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

#if UNITY_ANDROID 
            while(!Permission.HasUserAuthorizedPermission("android.permission.RECORD_AUDIO")) {
                Permission.RequestUserPermission("android.permission.RECORD_AUDIO");
                yield return new WaitForSeconds(1);
            }
#endif
            yield return null;

            InitializeAgent();

            muteSelfToggle.SetIsOnWithoutNotify(audioChat.MuteSelf);
            muteSelfToggle.onValueChanged.AddListener(value =>
                audioChat.MuteSelf = value);

            muteOthersToggle.SetIsOnWithoutNotify(audioChat.MuteEveryone);
            muteOthersToggle.onValueChanged.AddListener(value =>
                audioChat.MuteEveryone = value);
        }

        void InitializeAgent() {
            // We use the Mirror based network implementation
            var network = new MirrorNetwork();

            // For the input, we use 16KHz sampled every 10 milliseconds
            var input = new UniMicInput(0, 16000, 10);

            // The output is played using AudioSourceOutput.
            // Since a new output will have to be created for every peer, we use a factory.
            // AudioSourceOutput uses CircularAudioClip, so we pass that to the factory.
            var outputFactory = new AudioSourceOutput.Factory(new CircularAudioClip(input));

            // AudioChat instance is created using the input, output and network
            audioChat = new AudioChat(network, input, outputFactory);

            // We add some denoising and compression for the input
            // Denoising improves the audio quality captured by the mic.
            // Compression allows us to lower bandwidth usage.
            audioChat.InputFilters.Add(new GaussianAudioDenoising(2f, 2));
            audioChat.InputFilters.Add(new GZipAudioCompression());

            // When receiving peer audio, we must decompress the audio, since we're compressing
            // audio beofre sending.
            audioChat.OutputFilters.Add(new GZipAudioDecompression());

            // Setup some events to show relevant text
            audioChat.Network.OnCreatedChatroom += () => {
                ShowMessage($"Created Chatroom on Mirror!\nYou are Peer ID {audioChat.Network.OwnID}");
            };

            audioChat.Network.OnClosedChatroom += () => {
                ShowMessage("You closed the Mirror server! All peers have been kicked");
                foreach (var view in peerViews)
                    Destroy(view.Value.gameObject);
                peerViews.Clear();
            };

            audioChat.Network.OnChatroomCreationFailed += ex => {
                ShowMessage("Chatroom creation failed. Check Mirror logs for errors.");
            };

            audioChat.Network.OnJoinedChatroom += id => {
                ShowMessage("You are Peer ID " + id);
            };

            audioChat.Network.OnChatroomJoinFailed += ex => {
                ShowMessage(ex);
            };

            audioChat.Network.OnLeftChatroom += () => {
                ShowMessage("You left the chatroom");
                foreach (var view in peerViews)
                    Destroy(view.Value.gameObject);
                peerViews.Clear();
            };

            // When a peer joins, we instantiate a new peer view 
            audioChat.Network.OnPeerJoinedChatroom += id => {
                var view = Instantiate(peerViewTemplate, peerViewContainer);
                view.SetPeerID(id);
                peerViews.Add(id, view);

                // Initialize the views to show the right UI based on mute settings.
                view.AllowIncomingAudio = !audioChat.PerPeerSettings[id].muteThem;
                view.AllowOutgoingAudio = !audioChat.PerPeerSettings[id].muteSelf;

                // When the user uses the UI to change the mute settings, update the audio chat
                view.OnAllowIncomingAudioChange += value =>
                    audioChat.PerPeerSettings[id].muteThem = !value;

                view.OnAllowOutgoingAudioChange += value =>
                    audioChat.PerPeerSettings[id].muteSelf = !value;
            };

            // When a peer leaves, destroy the UI representing them
            audioChat.Network.OnPeerLeftChatroom += id => {
                if(peerViews.ContainsKey(id)) {
                    var peerViewInstance = peerViews[id];
                    Destroy(peerViewInstance.gameObject);
                    peerViews.Remove(id);
                }
            };

            // We start off with all muting off
            audioChat.MuteEveryone = false;
            audioChat.MuteSelf = false;
        }

        void Update() {
            if (audioChat == null || audioChat.PerPeerOutputs == null) return;

            foreach (var output in audioChat.PerPeerOutputs) {
                if (peerViews.ContainsKey(output.Key)) {
                    /*
                     * This is an inefficient way of showing a part of the 
                     * audio source spectrum. AudioSource.GetSpectrumData returns
                     * frequency values up to 24000 Hz in some cases. Most human
                     * speech is no more than 5000 Hz. Showing the entire spectrum
                     * will therefore lead to a spectrum where much of it doesn't
                     * change. So we take only the spectrum frequencies between
                     * the average human vocal range.
                     * 
                     * Great source of information here: 
                     * http://answers.unity.com/answers/158800/view.html
                     */
                    var size = 512;
                    var minVocalFrequency = 50;
                    var maxVocalFrequency = 8000;
                    var sampleRate = AudioSettings.outputSampleRate;
                    var frequencyResolution = sampleRate / 2 / size;

                    var audioSource = (output.Value as AudioSourceOutput).AudioSource;
                    var spectrumData = new float[size];
                    audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);

                    var indices = Enumerable.Range(0, size - 1).ToList();
                    var minVocalFrequencyIndex = indices.Min(x => (Mathf.Abs(x * frequencyResolution - minVocalFrequency), x)).x;
                    var maxVocalFrequencyIndex = indices.Min(x => (Mathf.Abs(x * frequencyResolution - maxVocalFrequency), x)).x;
                    var indexRange = maxVocalFrequencyIndex - minVocalFrequency;

                    spectrumData = spectrumData.Select(x => 1000 * x)
                        .ToList()
                        .GetRange(minVocalFrequency, indexRange)
                        .ToArray();
                    peerViews[output.Key].DisplaySpectrum(spectrumData);
                }
            }
        }

        void ShowMessage(object obj) {
            Debug.Log("GroupVoiceCall_MirrorSample:" + obj);
            chatroomMessage.text = obj.ToString();
        }
    }
}
#endif