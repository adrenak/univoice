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

            outputFactory = new DefaultAudioOutputFactory();

            agent = new VoiceChatAgent(network, input, outputFactory) {
                Mute = false
            };

            agent.ChatroomNetwork.OnChatroomCreated += () => {
                message.text = "Create success. Ask other device to Join using the same room name.";
            };
            agent.ChatroomNetwork.OnChatroomCreationFailed += ex => {
                message.text = "Chatroom Create failure. Try changing our internet conenctivity.";
            };
            agent.ChatroomNetwork.OnChatroomClosed += () => {
                Debug.Log("Chatroom shutdown");
                message.text = "Chatroom shutdown";
            };

            agent.ChatroomNetwork.OnJoiningFailed += ex =>
                message.text = "Failed to join chatroom";
            agent.ChatroomNetwork.OnJoined += id => {
                message.text = $"Joined chatroom {agent.ChatroomNetwork.CurrentChatroomName}. Your ID is {agent.ChatroomNetwork.OwnID}";
            };
            agent.ChatroomNetwork.OnLeft += () => {
                Debug.Log("You left the chatroom");
                message.text = "You left the chatroom";
            };
            agent.ChatroomNetwork.OnPeerJoined += id => {
                message.text = $"Peer #{id} has joined your room. Speak now!";
            };
            agent.ChatroomNetwork.OnPeerLeft += id => {
                var msg = $"Peer #{id} disconnected.";
                if (agent.ChatroomNetwork.PeerIDs.Count == 0)
                    msg += " No more peers.";
                message.text = msg;
                Debug.Log(msg);
            };
        }

        public void Create(InputField input) {
            message.text = "Initializing network...";
            agent.ChatroomNetwork.HostChatroom(input.text);
        }

        public void Join(InputField input) {
            message.text = "Joining network...";
            agent.ChatroomNetwork.JoinChatroom(input.text);
        }

        public void Leave() {
            if (agent == null) return;

            if (agent.CurrentMode == VoiceChatAgentMode.Host)
                agent.ChatroomNetwork.CloseChatroom();
            else
                agent.ChatroomNetwork.LeaveChatroom();
            message.text = "Left room";
        }

        private void OnApplicationQuit() {
            agent.Dispose();
        }
    }
}
