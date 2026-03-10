#if UNITY_NETCODE_GAMEOBJECTS
using UnityEngine;

using Adrenak.UniMic;
using Adrenak.UniVoice.Networks;
using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniVoice.Filters;

namespace Adrenak.UniVoice.Samples {
    /// <summary>
    /// To get this setup sample to work, ensure that you have done the following:
    /// - Import Netcode for GameObjects and add it to your project (UNIVOICE_NETCODE_GAMEOBJECTS is auto-defined when the package is present)
    /// - Add a NetworkManager to your scene and start as Host or Client
    /// - Add this component to your Unity project
    /// 
    /// This is a basic integration script that uses the following to setup UniVoice:
    /// - <see cref="NGOServer"/>, an implementation of <see cref="IAudioServer{T}"/> 
    /// - <see cref="NGOClient"/>, an implementation of <see cref="IAudioClient{T}"/> 
    /// - <see cref="UniMicInput"/>, an implementation of <see cref="IAudioInput"/> that captures audio from a mic
    /// - <see cref="EmptyAudioInput"/>, an implementation of <see cref="IAudioInput"/> 
    /// - <see cref="ConcentusEncodeFilter"/>, <see cref="ConcentusDecodeFilter"/> for audio encoding/decoding
    /// </summary>
    public class UniVoiceNGOSetupSample : MonoBehaviour {
        const string TAG = "[UniVoiceNGOSetupSample]";

        public static bool HasSetUp { get; private set; }
        public static IAudioServer<int> AudioServer { get; private set; }
        public static ClientSession<int> ClientSession { get; private set; }

#pragma warning disable CS0414
        [SerializeField] bool useConcentusEncodeAndDecode = true;
        [SerializeField] bool useVad = true;
#pragma warning restore

        void Start() {
            DontDestroyOnLoad(gameObject);
            if (HasSetUp) {
                Debug.unityLogger.Log(LogType.Log, TAG, "UniVoice is already set up. Ignoring...");
                return;
            }
            HasSetUp = Setup();
        }

        bool Setup() {
            Debug.unityLogger.Log(LogType.Log, TAG, "Trying to setup UniVoice with Netcode for GameObjects");

            bool failed = false;

            var setupAudioClient = SetupClientSession();
            if (!setupAudioClient) {
                Debug.unityLogger.Log(LogType.Error, TAG, "Could not setup UniVoice client.");
                failed = true;
            }

            var createdAudioServer = SetupAudioServer();
            if (!createdAudioServer) {
                Debug.unityLogger.Log(LogType.Error, TAG, "Could not setup UniVoice server.");
                failed = true;
            }

            if (!failed)
                Debug.unityLogger.Log(LogType.Log, TAG, "UniVoice successfully setup!");
            else
                Debug.unityLogger.Log(LogType.Error, TAG, "Refer to the notes on top of UniVoiceNGOSetupSample.cs for setup instructions.");
            return !failed;
        }

        bool SetupAudioServer() {
            AudioServer = new NGOServer(client as NGOClient);
            Debug.unityLogger.Log(LogType.Log, TAG, "Created NGOServer object");

            AudioServer.OnServerStart += () => {
                Debug.unityLogger.Log(LogType.Log, TAG, "Server started");
            };

            AudioServer.OnServerStop += () => {
                Debug.unityLogger.Log(LogType.Log, TAG, "Server stopped");
            };
            return true;
        }

        IAudioClient<int> client;

        bool SetupClientSession() {
            client = new NGOClient();
            client.OnJoined += (id, peerIds) => {
                Debug.unityLogger.Log(LogType.Log, TAG, $"You are Peer ID {id}");
            };

            client.OnLeft += () => {
                Debug.unityLogger.Log(LogType.Log, TAG, "You left the chatroom");
            };

            client.OnPeerJoined += id => {
                Debug.unityLogger.Log(LogType.Log, TAG, $"Peer {id} joined");
            };

            client.OnPeerLeft += id => {
                Debug.unityLogger.Log(LogType.Log, TAG, $"Peer {id} left");
            };

            Debug.unityLogger.Log(LogType.Log, TAG, "Created NGOClient object");

            IAudioInput input;
            Mic.Init();
            if (Mic.AvailableDevices.Count == 0) {
                Debug.unityLogger.Log(LogType.Log, TAG, "Device has no microphones. Will only be able to hear other clients.");
                input = new EmptyAudioInput();
            } else {
                var mic = Mic.AvailableDevices[0];
                mic.StartRecording(60);
                input = new UniMicInput(mic);
            }

            IAudioOutputFactory outputFactory = new StreamedAudioSourceOutput.Factory();

            ClientSession = new ClientSession<int>(client, input, () => {
                var output = StreamedAudioSourceOutput.New();
                output.gameObject.name = "StreamedAudioSourceOutput";
                return output;
            });
            Debug.unityLogger.Log(LogType.Log, TAG, "Created session");

            if (useVad) {
                ClientSession.InputFilters.Add(new SimpleVadFilter(new SimpleVad()));
            }

            if (useConcentusEncodeAndDecode) {
                ClientSession.InputFilters.Add(new ConcentusEncodeFilter());
                ClientSession.AddOutputFilter<ConcentusDecodeFilter>(() => new ConcentusDecodeFilter());
            }

            return true;
        }
    }
}
#endif
