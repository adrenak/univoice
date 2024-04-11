using UnityEngine;

namespace Adrenak.UniVoice.Outputs {
    /// <summary>
    /// This class feeds incoming segments of audio to an AudioBuffer 
    /// and plays the buffer's clip on an AudioSource. It also clears segments
    /// of the buffer based on the AudioSource's position.
    /// </summary>
    public class AudioSourceOutput : MonoBehaviour, IAudioOutput {
        const string TAG = "AudioSourceOutput";

        public AudioSource AudioSource { get; private set; }
        public CircularAudioClip CircularAudioClip { get; private set; }
        public int PlayPadding { get; set; } = 1;

        [System.Obsolete("Cannot use new keyword to create an instance. Use AudioSourceOutput.New() method instead")]
        public AudioSourceOutput() { }

        /// <summary>
        /// Creates a new instance using the dependencies.
        /// </summary>
        /// 
        /// <param name="buffer">
        /// The AudioBuffer that the streamer operates on.
        /// </param>
        /// 
        /// <param name="source">
        /// The AudioSource from where the incoming audio is played.
        /// </param>
        /// 
        /// <param name="minSegCount">
        /// The minimum number of audio segments <see cref="CircularAudioClip"/> 
        /// must have for the streamer to play the audio. This value is capped
        /// between 1 and <see cref="CircularAudioClip.SegCount"/> of the 
        /// <see cref="CircularAudioClip"/> passed.
        /// Default: 0. Results in the value being set to the max possible.
        /// </param>
        public static AudioSourceOutput New(CircularAudioClip buffer, AudioSource source) {
            var ctd = source.gameObject.AddComponent<AudioSourceOutput>();
            DontDestroyOnLoad(ctd.gameObject);

            source.loop = true;
            source.clip = buffer.AudioClip;
            source.Play();

            ctd.CircularAudioClip = buffer;
            ctd.AudioSource = source;

            Debug.unityLogger.Log(TAG, $"Created with the following params:" +
            $"buffer AudioClip channels: {buffer.AudioClip.channels}" +
            $"buffer AudioClip frequency: {buffer.AudioClip.frequency}" +
            $"buffer AudioClip samples: {buffer.AudioClip.samples}");

            return ctd;
        }

        /// <summary>
        /// This is to make sure that if a segment is missed, its previous 
        /// contents won't be played again when the clip loops back.
        /// </summary>
        private void Update() {
            if (AudioSource.clip == null) return;

            CircularAudioClip.UpdateReadIndex(AudioSource.timeSamples);

            if (CanPlay() && !AudioSource.isPlaying) {
                AudioSource.UnPause();
            }
            else if (!CanPlay() && AudioSource.isPlaying) {
                AudioSource.Pause();
            }
        }

        /// <summary>
        /// Feeds an incoming <see cref="ChatroomAudioSegment"/> into the audio buffer.
        /// </summary>
        /// <param name="segment"></param>
        public void Feed(AudioFrame segment) {
            CircularAudioClip.Write(segment.timestamp, Utils.Bytes.BytesToFloats(segment.samples));
        }

        /// <summary>
        /// Disposes the instance by deleting the GameObject of the component.
        /// </summary>
        public void Dispose() {
            Destroy(gameObject);
        }

        bool CanPlay() {
            if (CircularAudioClip.WriteIndex < CircularAudioClip.ReadIndex) return true;

            if (CircularAudioClip.ReadIndex != CircularAudioClip.Size)
                return CircularAudioClip.ReadIndex < CircularAudioClip.WriteIndex - PlayPadding;
            else
                return CircularAudioClip.ReadIndex < PlayPadding;
        }

        /// <summary>
        /// Creates <see cref="UniVoiceAudioSourceOutput"/> instances
        /// </summary>
        public class Factory : IAudioOutputFactory {
            readonly CircularAudioClip circularAudioClip;

            public Factory(CircularAudioClip circularAudioClip) {
                this.circularAudioClip = circularAudioClip;
            }

            public IAudioOutput Create(int samplingRate, int channelCount, int segmentLength) {
                return New(
                    circularAudioClip,
                    new GameObject($"AudioSourceOutput").AddComponent<AudioSource>()
                );
            }
        }
    }
}
