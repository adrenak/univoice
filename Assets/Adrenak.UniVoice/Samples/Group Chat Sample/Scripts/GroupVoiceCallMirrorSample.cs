#if UNIVOICE_MIRROR_NETWORK
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;

using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniMic;
using Adrenak.UnityOpus;

namespace Adrenak.UniVoice.Samples {
    public class GroupVoiceCallMirrorSample : MonoBehaviour {
        public Transform peerViewContainer;
        public PeerView peerViewTemplate;
        public Text chatroomMessage;
        public Toggle muteSelfToggle;
        public Toggle muteOthersToggle;

        ClientSession<int> session;
        Dictionary<int, PeerView> peerViews = new Dictionary<int, PeerView>();

        IEnumerator Start() {
            Mic.Init();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

#if UNITY_ANDROID 
            while(!Permission.HasUserAuthorizedPermission("android.permission.RECORD_AUDIO")) {
                Permission.RequestUserPermission("android.permission.RECORD_AUDIO");
                yield return new WaitForSeconds(1);
            }
#endif
            yield return null;

            InitializeSession();

            // Listen to changes in the toggles to deafen or mute all peers 
            muteSelfToggle.SetIsOnWithoutNotify(session.Client.YourVoiceSettings.deafenAll);
            muteSelfToggle.onValueChanged.AddListener(value => {
                session.Client.YourVoiceSettings.deafenAll = value;
                session.Client.SubmitVoiceSettings();
            });

            muteOthersToggle.SetIsOnWithoutNotify(session.Client.YourVoiceSettings.muteAll);
            muteOthersToggle.onValueChanged.AddListener(value => {
                session.Client.YourVoiceSettings.muteAll = value;
                session.Client.SubmitVoiceSettings();
            });
        }

        void InitializeSession() {
            // We create a server. If this code runs in server mode, MirrorServer will take care
            // or automatically handling all incoming messages.
            var server = new MirrorServer();

            // Since in this sample we use microphone input via UniMic, we first check if there
            // are any mic devices available.
            if (Mic.AvailableDevices.Count == 0)
                return;

            // Create a client for this device
            var client = new MirrorClient();

            // Get the first recording device that we have available and start it.
            // Then we create a UniMicInput instance that requires the mic object
            // For more info on UniMic refer to https://www.github.com/adrenak/unimic
            var mic = Mic.AvailableDevices[0];
            mic.StartRecording();
            var input = new UniMicInput(mic);

            // We want the incoming audio from peers to be played via the StreamedAudioSourceOutput
            // implementation of IAudioSource interface. So we get the factory for it.
            var outputFactory = new StreamedAudioSourceOutput.Factory();

            // With the client, input and output factory ready, we create create the client session
            session = new ClientSession<int>(client, input, outputFactory);

            // We add some filters to the input audio
            // - The first is audio blur, so that the audio that's been captured by this client
            // has lesser noise
            var outgoingBlur = new GaussianAudioBlur();

            // - The next one is the Opus encoder filter. This is VERY important. Without this the
            // outgoing data would be very large, usually by a factor of 10 or more.
            // To do this we first create the encoder, then use it to create an instance of 
            // of the OpusEncodeFilter class 
            var encoder = new Encoder(
                (SamplingFrequency)mic.SamplingFrequency,
                (NumChannels)mic.ChannelCount,
                OpusApplication.VoIP
            );
            var outgoingEncode = new OpusEncodeFilter(encoder);

            // Finally, we add both the input filters to the session. Note that session.InputFilters
            // is a list and the filters are run in the order they are added to it.
            session.InputFilters.Add(outgoingBlur);
            session.InputFilters.Add(outgoingEncode);

            // Next, for incoming audio we create some filters.
            // - first is the decoder, because the audio that we are expecting would be encoded.
            // So we create a decoder, use that to create a OpusDecodeFilter and add it to the sessions 
            // output filters
            var decoder = new Decoder(
                (SamplingFrequency)mic.SamplingFrequency, 
                (NumChannels)mic.ChannelCount
            );
            var incomingDecode = new OpusDecodeFilter(decoder);

            session.OutputFilters.Add(incomingDecode);

            // Subscribe to some server events 
            server.OnServerStart += () => {
                ShowMessage("Server started");
            };

            server.OnServerStop += () => {
                ShowMessage("Server stopped");
            };

            // We subscribe to some client events to show updates on the UI when you join or leave
            client.OnJoined += (id, peerIds) => {
                ShowMessage($"You are Peer ID {id} your peers are {string.Join(", ", peerIds)}");
            };

            client.OnLeft += () => {
                ShowMessage("You left the chatroom");
                foreach (var view in peerViews)
                    Destroy(view.Value.gameObject);
                peerViews.Clear();
            };

            // When a peer joins, we instantiate a new peer view 
            client.OnPeerJoined += id => {
                var view = Instantiate(peerViewTemplate, peerViewContainer);
                view.SetPeerID(id);

                // we listen to the changes in the individual toggle buttons
                // on this view to selectively mute or deafen the peer that
                // is represented by the view
                view.OnAllowIncomingAudioChange += x => {
                    client.YourVoiceSettings.SetMute(id, !x);
                    client.SubmitVoiceSettings();
                };
                view.OnAllowOutgoingAudioChange += x => {
                    client.YourVoiceSettings.SetDeaf(id, !x);
                    client.SubmitVoiceSettings();              
                };
                peerViews.Add(id, view);
            };

            // When a peer leaves, destroy the UI representing them
            client.OnPeerLeft += id => {
                if (peerViews.ContainsKey(id)) {
                    var peerViewInstance = peerViews[id];
                    Destroy(peerViewInstance.gameObject);
                    peerViews.Remove(id);
                }
            };
        }

        // Here we just show some audio visualization of incoming peer audio.
        void Update() {
            if (session == null) return;

            foreach (var output in session.PeerOutputs) {
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

                    var audioSource = (output.Value as StreamedAudioSourceOutput).Stream.UnityAudioSource;
                    var spectrumData = new float[size];
                    audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);

                    var indices = Enumerable.Range(0, size - 1).ToList();
                    var minVocalFrequencyIndex = indices.Min(x => (Mathf.Abs(x * frequencyResolution - minVocalFrequency), x)).x;
                    var maxVocalFrequencyIndex = indices.Min(x => (Mathf.Abs(x * frequencyResolution - maxVocalFrequency), x)).x;
                    var indexRange = maxVocalFrequencyIndex - minVocalFrequency;

                    // Using LINQ here to keep it short. But this generates a lot of garbage.
                    // If you're visualizing incoming audio data in your app/game, consider using some
                    // caching and memory allocation saving techniques
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