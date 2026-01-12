using UnityEngine;

using Adrenak.UniMic;
using Adrenak.UniVoice.Networks;
using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniVoice.Filters;

namespace Adrenak.UniVoice.Samples {
    /// <summary>
    /// To get this setup sample to work, ensure that you have done the following:
    /// - Import Mirror and add the UNIVOICE_NETWORK_MIRROR compilation symbol to your project
    /// - If you want to use RNNoise filter, import RNNoise4Unity into your project and add UNIVOICE_FILTER_RNNOISE4UNITY
    /// - Add this component to the first scene of your Unity project
    /// 
    /// *** More info on adding and activating non packaged dependencies is here: https://github.com/adrenak/univoice?tab=readme-ov-file#activating-non-packaged-dependencies ***
    /// 
    /// This is a basic integration script that uses the following to setup UniVoice:
    /// - <see cref="MirrorServer"/>, an implementation of <see cref="IAudioServer{T}"/> 
    /// - <see cref="MirrorClient"/>, an implementation of <see cref="IAudioClient{T}"/> 
    /// - <see cref="UniMicInput"/>, an implementation of <see cref="IAudioInput"/> that captures audio from a mic
    /// - <see cref="EmptyAudioInput"/>, an implementation of <see cref="IAudioInput"/> that is basically
    /// an idle audio input used when there is no input device
    /// - <see cref="RNNoiseFilter"/>, an implementation of <see cref="IAudioFilter"/> that removes noise from
    /// captured audio. 
    /// - <see cref="ConcentusEncodeFilter"/>, an implementation of <see cref="IAudioFilter"/> that encodes captured audio
    /// using Concentus (C# Opus) to reduce the size of audio frames
    /// - <see cref="ConcentusDecodeFilter"/>, an implementation of <see cref="IAudioFilter"/> that decodes incoming audio
    /// using Concentus to decode and make the audio frame playable.
    /// </summary>
    public class UniVoiceMirrorSetupSample : MonoBehaviour {
        const string TAG = "[BasicUniVoiceSetupSample]";

        /// <summary>
        /// Whether UniVoice has been setup successfully. This field will return true if the setup was successful.
        /// It runs on both server and client.
        /// </summary>
        public static bool HasSetUp { get; private set; }

        /// <summary>
        /// The server object.
        /// </summary>
        public static IAudioServer<int> AudioServer { get; private set; }

        /// <summary>
        /// The client session.
        /// </summary>
        public static ClientSession<int> ClientSession { get; private set; }

#pragma warning disable CS0414
        [SerializeField] bool useRNNoise4UnityIfAvailable = true;

        [SerializeField] bool useConcentusEncodeAndDecode = true;

        [SerializeField] bool useVad = true;
#pragma warning restore

        void Start() {
            if (HasSetUp) {
                Debug.unityLogger.Log(LogType.Log, TAG, "UniVoice is already set up. Ignoring...");
                return;
            }
            HasSetUp = Setup();
        }

        bool Setup() {
            Debug.unityLogger.Log(LogType.Log, TAG, "Trying to setup UniVoice");

            bool failed = false;

            // Set setup the AudioServer and ClientSession on ALL builds. This means that you'd
            // have a ClientSession on a dedicated server, even though there's not much you can do with it.
            // Similarly, a client would also have an AudioServer object. But it would just be inactive.
            // This sample is for ease of use and to get something working quickly, so we don't bother
            // with these minor details. Note that doing so does not have any performance implications
            // so you can do this, so you could keep this approach without any tradeoffs.
            var createdAudioServer = SetupAudioServer();
            if (!createdAudioServer) {
                Debug.unityLogger.Log(LogType.Error, TAG, "Could not setup UniVoice server.");
                failed = true;
            }

            var setupAudioClient = SetupClientSession();
            if (!setupAudioClient) {
                Debug.unityLogger.Log(LogType.Error, TAG, "Could not setup UniVoice client.");
                failed = true;
            }

            if (!failed)
                Debug.unityLogger.Log(LogType.Log, TAG, "UniVoice successfully setup!");
            else
                Debug.unityLogger.Log(LogType.Error, TAG, $"Refer to the notes on top of {typeof(UniVoiceMirrorSetupSample).Name}.cs for setup instructions.");
            return !failed;
        }

        bool SetupAudioServer() {
#if MIRROR
            // ---- CREATE AUDIO SERVER AND SUBSCRIBE TO EVENTS TO PRINT LOGS ----
            // We create a server. If this code runs in server mode, MirrorServer will take care
            // or automatically handling all incoming messages. On a device connecting as a client,
            // this code doesn't do anything.
            AudioServer = new MirrorServer();
            Debug.unityLogger.Log(LogType.Log, TAG, "Created MirrorServer object");

            AudioServer.OnServerStart += () => {
                Debug.unityLogger.Log(LogType.Log, TAG, "Server started");
            };

            AudioServer.OnServerStop += () => {
                Debug.unityLogger.Log(LogType.Log, TAG, "Server stopped");
            };
            return true;
#else
            Debug.unityLogger.Log(LogType.Error, TAG, "MirrorServer implementation not found!");
            return false;
#endif
        }

        bool SetupClientSession() {
#if MIRROR
            // ---- CREATE AUDIO CLIENT AND SUBSCRIBE TO EVENTS ----
            IAudioClient<int> client = new MirrorClient();
            client.OnJoined += (id, peerIds) => {
                Debug.unityLogger.Log(LogType.Log, TAG, $"You are Peer ID {id}");
            };

            client.OnLeft += () => {
                Debug.unityLogger.Log(LogType.Log, TAG, "You left the chatroom");
            };

            // When a peer joins, we instantiate a new peer view 
            client.OnPeerJoined += id => {
                Debug.unityLogger.Log(LogType.Log, TAG, $"Peer {id} joined");
            };

            // When a peer leaves, destroy the UI representing them
            client.OnPeerLeft += id => {
                Debug.unityLogger.Log(LogType.Log, TAG, $"Peer {id} left");
            };

            Debug.unityLogger.Log(LogType.Log, TAG, "Created MirrorClient object");

            // ---- CREATE AUDIO INPUT ----
            IAudioInput input;
            // Since in this sample we use microphone input via UniMic, we first check if there
            // are any mic devices available.
            Mic.Init(); // Must do this to use the Mic class
            if (Mic.AvailableDevices.Count == 0) {
                Debug.unityLogger.Log(LogType.Log, TAG, "Device has no microphones." +
                "Will only be able to hear other clients, cannot send any audio.");
                input = new EmptyAudioInput();
                Debug.unityLogger.Log(LogType.Log, TAG, "Created EmptyAudioInput");
            }
            else {
                // Get the first recording device that we have available and start it.
                // Then we create a UniMicInput instance that requires the mic object
                // For more info on UniMic refer to https://www.github.com/adrenak/unimic
                var mic = Mic.AvailableDevices[0];
                mic.StartRecording(60);
                Debug.unityLogger.Log(LogType.Log, TAG, "Started recording with Mic device named." +
                mic.Name + $" at frequency {mic.SamplingFrequency} with frame duration {mic.FrameDurationMS} ms.");
                input = new UniMicInput(mic);
                Debug.unityLogger.Log(LogType.Log, TAG, "Created UniMicInput");
            }

            // ---- CREATE AUDIO OUTPUT FACTORY ----
            IAudioOutputFactory outputFactory;
            // We want the incoming audio from peers to be played via the StreamedAudioSourceOutput
            // implementation of IAudioSource interface. So we get the factory for it.
            outputFactory = new StreamedAudioSourceOutput.Factory();
            Debug.unityLogger.Log(LogType.Log, TAG, "Using StreamedAudioSourceOutput.Factory as output factory");

            // ---- CREATE CLIENT SESSION AND ADD FILTERS TO IT ----
            // With the client, input and output factory ready, we create create the client session
            ClientSession = new ClientSession<int>(client, input, outputFactory);
            Debug.unityLogger.Log(LogType.Log, TAG, "Created session");

#if UNIVOICE_FILTER_RNNOISE4UNITY
            if(useRNNoise4UnityIfAvailable) {
                // RNNoiseFilter to remove noise from captured audio
                session.InputFilters.Add(new RNNoiseFilter());
                Debug.unityLogger.Log(LogType.Log, TAG, "Registered RNNoiseFilter as an input filter");
            }
#endif

            if (useVad) {
                // We add the VAD filter after RNNoise. 
                // This way lot of the background noise has been removed, VAD is truly trying to detect voice
                ClientSession.InputFilters.Add(new SimpleVadFilter(new SimpleVad()));
            }

            if (useConcentusEncodeAndDecode) {
                // ConcentureEncoder filter to encode captured audio that reduces the audio frame size
                ClientSession.InputFilters.Add(new ConcentusEncodeFilter());
                Debug.unityLogger.Log(LogType.Log, TAG, "Registered ConcentusEncodeFilter as an input filter");

                // For incoming audio register the ConcentusDecodeFilter to decode the encoded audio received from other clients 
                ClientSession.AddOutputFilter<ConcentusDecodeFilter>(() => new ConcentusDecodeFilter());
                Debug.unityLogger.Log(LogType.Log, TAG, "Registered ConcentusDecodeFilter as an output filter");
            }

            return true;
#else
            Debug.unityLogger.Log(LogType.Error, TAG, "MirrorClient implementation not found!");
            return false;
#endif
        }
    }
}