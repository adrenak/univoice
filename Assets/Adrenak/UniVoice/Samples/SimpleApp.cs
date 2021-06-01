using System.Linq;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Adrenak.UniMic;

namespace Adrenak.UniVoice.Samples {
    [RequireComponent(typeof(AudioSource))]
    public class SimpleApp : MonoBehaviour {
        public Text message;
        public Text sendingIndex;
        public Text receivingIndex;
        public Text vol;

        IAudioInput input;
        IChatroomNetwork network;
        IAudioOutputFactory outputFactory;
        OutputLifecycle peerOutputLifecycle;
        VoiceChatAgent agent;

        void Start() {
            Screen.sleepTimeout = -1;
            Application.runInBackground = true;
            AskForPermission();
            Init();
        }

        public void AskForPermission() {
#if UNITY_ANDROID && !UNITY_EDITOR
			if(!AndroidPermissionsManager.IsPermissionGranted("android.permission.RECORD_AUDIO")){
				AndroidPermissionsManager.RequestPermission("android.permission.RECORD_AUDIO", new AndroidPermissionCallback(
					onGrantedCallback => {
						message.text = "RECORD_AUDIO permission granted!";
					},
					onDeniedCallback => {
						message.text = "Please grant RECORD_AUDIO permission! Click the mic button to do so.";
					}
				));
            }
#endif
        }

        public void Init() {
#if UNITY_ANDROID && !UNITY_EDITOR
			if (!AndroidPermissionsManager.IsPermissionGranted("android.permission.RECORD_AUDIO")) {
				message.text = "Please grant RECORD_AUDIO permission! Click the mic button to do so.";
				return;
			}
#endif
            network = new AirPeerChatroomNetwork("ws://167.71.17.13:11000");

            input = new UniMicAudioInput(Mic.Instance);
            if (!Mic.Instance.IsRecording)
                Mic.Instance.StartRecording(16000, 100);



            peerOutputLifecycle = new OutputLifecycle(
                (id, frequency, channels) => {
                    return DefaultAudioOutput.New(
                        new AudioBuffer(frequency, channels, input.GetSegmentLength(), 5, $"Peer #{id} Clip"),
                        new GameObject($"UniVoice Peer #{id}").AddComponent<AudioSource>(),
                        3
                    );
                },
                iAudioOutput => Destroy((iAudioOutput as DefaultAudioOutput).gameObject)
            );

            agent = new VoiceChatAgent(network, input, outputFactory) {
                Mute = false
            };

            agent.Network.OnChatroomCreated += () => {
                message.text = "Create success. Ask other device to Join using the same room name.";
            };
            agent.Network.OnChatroomCreationFailed += ex => {
                message.text = "Chatroom Create failure. Try changing our internet conenctivity.";
            };
            agent.Network.OnChatroomClosed += () => {
                Debug.Log("Chatroom shutdown");
                message.text = "Chatroom shutdown";
            };

            agent.Network.OnJoiningFailed += ex =>
                message.text = "Failed to join chatroom";
            agent.Network.OnJoined += id => {
                message.text = $"Joined chatroom {agent.Network.CurrentChatroomName}. Your ID is {agent.Network.OwnID}";
            };
            agent.Network.OnLeft += () => {
                Debug.Log("You left the chatroom");
                message.text = "You left the chatroom";
            };
            agent.Network.OnPeerJoined += id => {
                message.text = $"Peer #{id} has joined your room. Speak now!";
            };
            agent.Network.OnPeerLeft += id => {
                var msg = $"Peer #{id} disconnected.";
                if (agent.Network.PeerIDs.Count == 0)
                    msg += " No more peers.";
                message.text = msg;
                Debug.Log(msg);
            };
        }

        public void Create(InputField input) {
            message.text = "Initializing network...";
            agent.Network.HostChatroom(input.text);
        }

        public void Join(InputField input) {
            message.text = "Joining network...";
            agent.Network.JoinChatroom(input.text);
        }

        public void Leave() {
            if (agent == null) return;

            if (agent.CurrentMode == VoiceChatAgentMode.Host)
                agent.Network.CloseChatroom();
            else
                agent.Network.LeaveChatroom();
            message.text = "Left room";
        }

        private void OnApplicationQuit() {
            agent.Dispose();
        }
    }
}
