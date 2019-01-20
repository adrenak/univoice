using Byn.Net;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;
using System.Collections;
using Adrenak.UniMic;
using UnityEngine;
using UnityEngine.UI;

namespace Adrenak.UniVoice.Examples {
	[RequireComponent(typeof(AudioSource))]
	public class SimpleApp : MonoBehaviour {
		public UnityEvent OnAmbientVolumeCalculationStart;
		public UnityEvent OnAmbientVolumeCalculationEnd;

		public Text message;
		public Text sendingIndex;
		public Text receivingIndex;
		public Text gate;
		public Text vol;

		Voice voice;
		float m_AmbientVolume;

		void Start() {
			Screen.sleepTimeout = -1;
			Application.runInBackground = true;
			AskForPermission();
		}

		public void AskForPermission() {
#if UNITY_ANDROID && !UNITY_EDITOR
			AndroidPermissionsManager.RequestPermission("android.permission.RECORD_AUDIO", new AndroidPermissionCallback(
				onGrantedCallback => {
					message.text = "RECORD_AUDIO permission granted!";
				},
				onDeniedCallback => {
					message.text = "Please grant RECORD_AUDIO permission! Click the mic button to do so.";
				}
			));
#endif
		}

		public void Init() {
#if UNITY_ANDROID && !UNITY_EDITOR
			if (!AndroidPermissionsManager.IsPermissionGranted("android.permission.RECORD_AUDIO")) {
				message.text = "Please grant RECORD_AUDIO permission! Click the mic button to do so.";
				return;
			}
#endif

			voice = Voice.New(GetComponent<AudioSource>());
			voice.Speaking = true;

			voice.OnJoin += delegate (ConnectionId id) {
				message.text = "Someone has joined your room. Speak now!";
			};

			voice.OnLeave += delegate (ConnectionId id) {
				message.text = "Peer disconnected. You can't talk to anyone now. You have no friends either.";
			};

			voice.OnGetVoiceSegment += delegate (int index, float[] segment) {
				receivingIndex.text = "Received index : \n" + index;
			};

			voice.OnSendVoiceSegment += delegate (int index, float[] segment) {
				sendingIndex.text = "Sent index : \n" + index;
				vol.text = segment.Max().ToString();
			};
		}

		public void Create(InputField input) {
			Init();
			message.text = "Initializing network...";
			voice.Create(input.text, status => {
				if (status)
					message.text = "Create success. Ask other device to Join using the same room name.";
				else
					message.text = "Could not create room. Try 1) another room name 2) on cellphone data.";
			});
		}

		void asd() {
			// USER 1
			voice.Speaking = true;
			voice.Create("MY_VOICE_CHAT_ROOM", success => {
				if (success)
					Debug.Log("Started voice chat");
			});
			voice.OnJoin += id => {
				Debug.Log("Someone has joined the chat");
			};

			// USER 2
			voice.Speaking = true;
			voice.Join("MY_VOICE_CHAT_ROOM", success => {
				if (success)
					Debug.Log("Joined voice chat");
			});
		}

		public void Join(InputField input) {
			Init();
			message.text = "Joining network...";
			voice.Join(input.text, status => {
				if (status)
					message.text = "Join successful. You can talk now.";
				else
					message.text = "Could not join. Make sure the room name is correct. Try on cellphone data.";
			});
		}

		public void Leave() {
			voice.Leave();
			message.text = "Left room";
		}
	}
}
