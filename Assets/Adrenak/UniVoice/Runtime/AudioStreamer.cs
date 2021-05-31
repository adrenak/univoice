using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Adrenak.UniVoice {
    /// <summary>
    /// This class feeds incoming segments of audio to an AudioBuffer and plays the buffer's clip on 
    /// an AudioSource. It also clears segments of the buffer based on the AudioSource's current position.
    /// </summary>
    public class AudioStreamer : MonoBehaviour {
        public enum Status {
            Ahead,
            Current,
            Behind
        }

        Dictionary<int, Status> segments = new Dictionary<int, Status>();
        int GetSegmentCountByStatus(Status status) {
            var matches = segments.Where(x => x.Value == status);
            if (matches == null) return 0;
            return matches.Count();
        }

        public AudioBuffer AudioBuffer { get; private set; }
        public AudioSource AudioSource { get; private set; }
        public int MinSegCount { get; private set; }

        // Prevent contruction using 'new' as this is a MonoBehaviour class
        AudioStreamer() { }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="buffer">The AudioBuffer that the streamer operates on.</param>
        /// <param name="source">The AudioSource from where the incomming audio is played.</param>
        /// <param name="minSegCount">The minimum number of audio segments the internal <see cref="AudioBuffer"/> must have
        /// for the streamer to play the audio. This value is capped between 1 and <see cref="AudioBuffer.SegCount"/> of the 
        /// <see cref="AudioBuffer"/> passed. Default: 0 which results in the value being set to the max possible</param>
        /// <returns></returns>
        public static AudioStreamer New(AudioBuffer buffer, AudioSource source, int minSegCount = 0) {
            var cted = source.gameObject.AddComponent<AudioStreamer>();
            DontDestroyOnLoad(cted.gameObject);

            source.loop = true;
            source.clip = buffer.AudioClip;

            if (minSegCount != 0)
                cted.MinSegCount = Mathf.Clamp(minSegCount, 1, buffer.SegCount);
            else
                cted.MinSegCount = buffer.SegCount;
            cted.AudioBuffer = buffer;
            cted.AudioSource = source;

            return cted;
        }

        int lastIndex = -1;
        /// <summary>
        /// Runs every frame to see if the AudioSource has just moved to a new segment
        /// so that the previous one can be set to zero (silence). This is to make sure 
        /// that if a segment is missed, its previous contents won't be played again 
        /// when the clip loops.
        /// </summary>
        private void Update() {
            if (AudioSource.clip == null) return;

            var currentIndex = (int)(AudioSource.Position() * AudioBuffer.SegCount);

            // If we have moved to a new segment, clear the last one
            if (lastIndex != currentIndex) {
                segments.EnsureKey(lastIndex, Status.Behind);
                segments.EnsureKey(currentIndex, Status.Current);

                AudioBuffer.Clear(lastIndex);


                lastIndex = currentIndex;
            }

            var readyCount = GetSegmentCountByStatus(Status.Ahead);
            if (readyCount == 0)
                AudioSource.mute = true;
            else if (readyCount >= MinSegCount) {
                AudioSource.mute = false;
                if (!AudioSource.isPlaying)
                    AudioSource.Play();
            }
        }

        /// <summary>
        /// Streams an audio segment to the streamer, which feeds it to its buffer.
        /// </summary>
        /// <param name="index">The index of the segment, as reported by the mic to know 
        /// the absolute position of the segment on the buffer
        /// </param>
        /// <param name="audioSamples">The audio samples to be added to the buffer for playback</param>
        public void Stream(int index, float[] audioSamples) {
            // If we already have this index, don't bother
            // It's been passed already without playing.
            if (segments.ContainsKey(index)) return;

            // Don't write to the same segment index that we are reading
            int currentlyPlayingIndex = Mathf.Clamp((int)(AudioSource.Position() * AudioBuffer.SegCount), 0, AudioBuffer.SegCount - 1);
            var addToBufferIndex = AudioBuffer.GetNormalizedIndex(index);
            if (currentlyPlayingIndex == addToBufferIndex) return;

            // Finally write into the buffer 
            segments.Add(index, Status.Ahead);
            AudioBuffer.Write(index, audioSamples);
        }
    }
}
