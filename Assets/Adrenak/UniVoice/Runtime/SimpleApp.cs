//#define UNITY_ANDROID
//#undef UNITY_EDITOR
using System.Linq;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Adrenak.UniMic;

namespace Adrenak.UniVoice.Examples {
	[RequireComponent(typeof(AudioSource))]
	public class SimpleApp : MonoBehaviour {
		public Text message;
		public Text sendingIndex;
		public Text receivingIndex;
		public Text vol;

		VoiceChatAgent voice;
		float m_AmbientVolume;

		void Start() {
			Screen.sleepTimeout = -1;
			Application.runInBackground = true;
			AskForPermission();
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

			voice = VoiceChatAgent.New("ws://167.71.17.13:11000/", UniMicAudioInput.New());
			(voice.AudioInput as UniMicAudioInput).StartRecording(16000, 1600);

            voice.Mute = false;

			voice.OnCreateChatroom += () => {
				message.text = "Create success. Ask other device to Join using the same room name.";
			};
			voice.OnCouldNotCreeateChatroom += ex => {
				message.text = "Chatroom Create failure. Try changing our internet conenctivity.";
			};
			voice.OnShutdownChatroom += () => {
				message.text = "Chatroom shutdown";
			};


			voice.OnJoined += id => {
				message.text = $"Joined chatroom {voice.ChatRoomName}. Your ID is {voice.ID}";
			};
			voice.OnLeft += () => {
				message.text = "You left the chatroom";
			};
			voice.OnPeerJoined += id => {
				message.text = $"Peer #{id} has joined your room. Speak now!";
			};
			voice.OnPeerLeft += id => {
				message.text = $"Peer #{id} disconnected. You can't talk to anyone now.";
			};

			voice.OnGetAudio += delegate (short id, int index, float[] segment) {
				receivingIndex.text = "Received index : \n" + index;
			};
			voice.OnSendAudio += delegate (short[] id, int index, float[] segment) {
				sendingIndex.text = "Sent index : \n" + index;
				vol.text = segment.Max().ToString();
			};
		}

        private void Voice_OnLeft() {
            throw new System.NotImplementedException();
        }

        public void Create(InputField input) {
			Init();
			message.text = "Initializing network...";

			voice.CreateChatroom(input.text);
		}

		public void Join(InputField input) {
			Init();
			message.text = "Joining network...";
			voice.JoinChatroom(input.text);
		}

		public void Leave() {
			if (voice.MyMode == VoiceChatAgent.Mode.Host)
				voice.ShutdownChatroom();
			else
				voice.LeaveChatroom();
			message.text = "Left room";
		}
	}
}
