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

            IAudioInput mic = UniMicAudioInput.New();
            (mic as UniMicAudioInput).StartRecording(16000, 100);

            voice = new VoiceChatAgent(new AirPeerChatroomNetwork("ws://167.71.17.13:11000"), mic);
            voice.AudioOutputProvider = (id, frequency, channels) => {
                var segDataLen = mic.Frequency * mic.ChannelCount / mic.SegmentRate;
                var segCount = 5;
                var audioSource = new GameObject($"UniVoice Peer #{id}").AddComponent<AudioSource>();
                var streamer = DefaultAudioOutput.New(
                    new AudioBuffer(frequency, channels, segDataLen, segCount, $"Peer #{id} Clip"),
                    audioSource,
                    3
                );
                return streamer;
            };

            voice.Mute = false;

            voice.Network.OnChatroomCreated += () => {
                message.text = "Create success. Ask other device to Join using the same room name.";
            };
            voice.Network.OnChatroomCreationFailed += ex => {
                message.text = "Chatroom Create failure. Try changing our internet conenctivity.";
            };
            voice.Network.OnChatroomClosed += () => {
                message.text = "Chatroom shutdown";
            };


            voice.Network.OnJoined += id => {
                message.text = $"Joined chatroom {voice.ChatRoomName}. Your ID is {voice.ID}";
            };
            voice.Network.OnLeft += () => {
                message.text = "You left the chatroom";
            };
            voice.Network.OnPeerJoined += id => {
                message.text = $"Peer #{id} has joined your room. Speak now!";
            };
            voice.Network.OnPeerLeft += id => {
                message.text = $"Peer #{id} disconnected. You can't talk to anyone now.";
            };

            voice.Network.OnAudioReceived += delegate (short id, int index, int frequency, int channels, float[] segment) {
                receivingIndex.text = "Received index : \n" + index;
            };
            voice.Network.OnAudioSent += delegate (short id, int index, int frequency, int channels, float[] segment) {
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

            voice.Network.CreateChatroom(input.text);
        }

        public void Join(InputField input) {
            Init();
            message.text = "Joining network...";
            voice.Network.JoinChatroom(input.text);
        }

        public void Leave() {
            if (voice == null) return;

            if (voice.CurrentMode == VoiceChatAgentMode.Host)
                voice.Network.CloseChatroom();
            else
                voice.Network.LeaveChatroom();
            message.text = "Left room";
        }
    }
}
