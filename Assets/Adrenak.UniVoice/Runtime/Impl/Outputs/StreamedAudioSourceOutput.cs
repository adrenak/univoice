using Adrenak.UniMic;

using UnityEngine;

namespace Adrenak.UniVoice.Outputs {
    /// <summary>
    /// An implementation of <see cref="IAudioOutput"/> that plays
    /// peer audio using StreamedAUdioSource, which is included in UniMic
    /// </summary>
    [RequireComponent(typeof(StreamedAudioSource))]
    public class StreamedAudioSourceOutput : MonoBehaviour, IAudioOutput {
        const string TAG = "[StreamedAudioSourceOutput]";

        public StreamedAudioSource Stream { get; private set; }

        [System.Obsolete("Cannot use new keyword to create an instance. Use the .New() method instead")]
        public StreamedAudioSourceOutput() { }

        /// <summary>
        /// Creates a new instance using the dependencies.
        /// </summary>
        public static StreamedAudioSourceOutput New() {
            var go = new GameObject("StreamedAudioSourceOutput");
            DontDestroyOnLoad(go);
            var cted = go.AddComponent<StreamedAudioSourceOutput>();
            cted.Stream = go.GetComponent<StreamedAudioSource>();
            Debug.unityLogger.Log(LogType.Log, TAG, "StreamedAudioSource created");
            return cted;
        }

        /// <summary>
        /// Feeds an incoming <see cref="ChatroomAudioSegment"/> into the audio buffer.
        /// </summary>
        /// <param name="frame"></param>
        public void Feed(AudioFrame frame) {
            Stream.Feed(frame.frequency, frame.channelCount, Utils.Bytes.BytesToFloats(frame.samples), true);
        }

        /// <summary>
        /// Disposes the instance by deleting the GameObject of the component.
        /// </summary>
        public void Dispose() {
            Debug.unityLogger.Log(LogType.Log, TAG, "Disposing StreamedAudioSource");
            Destroy(gameObject);
        }

        /// <summary>
        /// Creates <see cref="UniVoiceAudioSourceOutput"/> instances
        /// </summary>
        public class Factory : IAudioOutputFactory {
            public IAudioOutput Create() {
                return New();
            }
        }
    }
}
