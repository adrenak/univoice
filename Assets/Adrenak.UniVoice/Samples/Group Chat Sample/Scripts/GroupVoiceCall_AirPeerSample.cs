#if UNIVOICE_AIRPEER_NETWORK
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
using System.Net.Sockets;
using System.Net;

namespace Adrenak.UniVoice.Samples {
    public class GroupVoiceCall_AirPeerSample : MonoBehaviour {
        public string signallingServerUrl = "localhost:12776";
        public string[] iceServerUrls = new string[] {
            "stun:stun.l.google.com:19302"
        };

        [Header("Menu")]
        public GameObject menuGO;
        public InputField roomNameInput;
        public InputField signallingServerUrlInput;
        public Button hostButton;
        public Button joinButton;
        public Text menuMessage;

        [Header("Chatroom")]
        public GameObject chatroomGO;
        public Transform peerViewContainer;
        public PeerView peerViewTemplate;
        public Text chatroomMessage;
        public Toggle muteSelfToggle;
        public Toggle muteOthersToggle;
        public Button leaveButton;

        AudioChat audioChat;
        Dictionary<int, PeerView> peerViews = new Dictionary<int, PeerView>();

        IEnumerator Start() {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Debug.Log("Local IP Address: " + GetLocalIPv4Address());

#if UNITY_ANDROID 
            while (!Permission.HasUserAuthorizedPermission("android.permission.RECORD_AUDIO")) {
                Permission.RequestUserPermission("android.permission.RECORD_AUDIO");
                yield return new WaitForSeconds(1);
            }
#endif
            yield return null;

            InitializeInput();
            InitializeAgent();

            menuGO.SetActive(true);
            chatroomGO.SetActive(false);

            signallingServerUrlInput.onValueChanged.AddListener(x => signallingServerUrl = x);

            muteSelfToggle.SetIsOnWithoutNotify(audioChat.MuteSelf);
            muteSelfToggle.onValueChanged.AddListener(value =>
                audioChat.MuteSelf = value);

            muteOthersToggle.SetIsOnWithoutNotify(audioChat.MuteEveryone);
            muteOthersToggle.onValueChanged.AddListener(value =>
                audioChat.MuteEveryone = value);
        }

        void InitializeInput() {
            hostButton.onClick.AddListener(HostChatroom);
            joinButton.onClick.AddListener(JoinChatroom);
            leaveButton.onClick.AddListener(ExitChatroom);
        }

        void InitializeAgent() {
            // We use the Telepathy based network implementation
            var network = new AirPeerNetwork(signallingServerUrl, iceServerUrls);

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
            audioChat.InputFilters.Add(new GaussianAudioDenoising(1f, 2));
            audioChat.InputFilters.Add(new GZipAudioCompression());

            // When receiving peer audio, we must decompress the audio, since we're compressing
            // audio beofre sending.
            audioChat.OutputFilters.Add(new GZipAudioDecompression());

            // Setup some events to show relevant text
            audioChat.Network.OnCreatedChatroom += () => {
                menuGO.SetActive(false);
                chatroomGO.SetActive(true);
                ShowMessage($"Created Chatroom on Telepathy!\nYou are Peer ID {audioChat.Network.OwnID}");
            };

            audioChat.Network.OnClosedChatroom += () => {
                ShowMessage("You closed the Telepathy server! All peers have been kicked");
                foreach (var view in peerViews)
                    Destroy(view.Value.gameObject);
                peerViews.Clear();
                menuGO.SetActive(true);
                chatroomGO.SetActive(false);
            };

            audioChat.Network.OnChatroomCreationFailed += ex => {
                ShowMessage("Chatroom creation failed. Check Telepathy logs for errors.");
            };

            audioChat.Network.OnJoinedChatroom += id => {
                menuGO.SetActive(false);
                chatroomGO.SetActive(true);
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
                menuGO.SetActive(true);
                chatroomGO.SetActive(false);
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
                if (peerViews.ContainsKey(id)) {
                    var peerViewInstance = peerViews[id];
                    Destroy(peerViewInstance.gameObject);
                    peerViews.Remove(id);
                }
            };

            // We start off with all muting off
            audioChat.MuteEveryone = false;
            audioChat.MuteSelf = false;
        }

        public static string GetLocalIPv4Address() {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address.ToString();
            }
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

        void HostChatroom() {
            var roomName = roomNameInput.text;
            audioChat.Network.HostChatroom(roomName);
        }

        void JoinChatroom() {
            var roomName = signallingServerUrlInput.text;
            if (string.IsNullOrEmpty(roomName))
                audioChat.Network.JoinChatroom();
            else
                audioChat.Network.JoinChatroom(roomName);
        }

        void ExitChatroom() {
            if (audioChat.CurrentMode == AudioChatMode.Host)
                audioChat.Network.CloseChatroom();
            else if (audioChat.CurrentMode == AudioChatMode.Guest)
                audioChat.Network.LeaveChatroom();
        }

        void ShowMessage(object obj) {
            Debug.Log("GroupVoiceChat_TelepathySample: " + obj);
            menuMessage.text = obj.ToString();
            if (audioChat.CurrentMode != AudioChatMode.Unconnected)
                chatroomMessage.text = obj.ToString();
        }
    }
}
#endif