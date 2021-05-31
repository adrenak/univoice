using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Adrenak.UniVoice {
    /*
     * This is a standard implementaiton of IAudioOutput that comes with UniVoice.
     * You can create your own implementations.
     */
    /// <summary>
    /// This class feeds incoming segments of audio to an AudioBuffer and plays the buffer's clip on 
    /// an AudioSource. It also clears segments of the buffer based on the AudioSource's current position.
    /// </summary>
    public class DefaultAudioOutput : MonoBehaviour, IAudioOutput {
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
        DefaultAudioOutput() { }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="buffer">The AudioBuffer that the streamer operates on.</param>
        /// <param name="source">The AudioSource from where the incomming audio is played.</param>
        /// <param name="minSegCount">The minimum number of audio segments the internal <see cref="AudioBuffer"/> must have
        /// for the streamer to play the audio. This value is capped between 1 and <see cref="AudioBuffer.SegCount"/> of the 
        /// <see cref="AudioBuffer"/> passed. Default: 0 which results in the value being set to the max possible</param>
        /// <returns></returns>
        public static DefaultAudioOutput New(AudioBuffer buffer, AudioSource source, int minSegCount = 0) {
            var cted = source.gameObject.AddComponent<DefaultAudioOutput>();
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
        public void Feed(int index, int frequency, int channelCount, float[] audioSamples) {
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

        public void Dispose() {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Used to put manage irregular, out of order and skipped audio segments for better playback.
    /// </summary>
    public class AudioBuffer {
        public AudioClip AudioClip { get; private set; }
        public int SegCount { get; private set; }
        public int SegDataLen { get; private set; }

        // Holds the first valid segment index received by the buffer
        int startIndex;

        /// <summary>
        /// Create an instance
        /// </summary>
        /// <param name="frequency">The frequency of the audio segment</param>
        /// <param name="channels">Number of channels in the segments</param>
        /// <param name="segDataLen">Segment length. Remains constant throughout the stream</param>
        /// <param name="segCount">The total number of segments stored as buffer. Default: 3 </param>
        public AudioBuffer(int frequency, int channels, int segDataLen, int segCount = 3, string clipName = null) {
            clipName = clipName ?? "clip";
            AudioClip = AudioClip.Create(clipName, segDataLen * segCount, channels, frequency, false);

            startIndex = -1;
            SegDataLen = segDataLen;
            SegCount = segCount;
        }

        /// <summary>
        /// Feed an audio segment to the buffer.
        /// </summary>
        /// <param name="absoluteIndex">The absolute index of the audio segment from the Microphone.</param>
        /// <param name="audioSegment">Float array representation of the audio</param>
        public bool Write(int absoluteIndex, float[] audioSegment) {
            // Reject if the segment length is wrong
            if (audioSegment.Length != SegDataLen) return false;

            if (absoluteIndex < 0 || absoluteIndex < startIndex) return false;

            // If this is the first segment fed
            if (startIndex == -1) startIndex = absoluteIndex;

            // Convert the absolute index into a looped-around index
            var localIndex = GetNormalizedIndex(absoluteIndex);

            // Set the segment at the clip data at the right index
            if (localIndex >= 0)
                AudioClip.SetData(audioSegment, localIndex * SegDataLen);
            return true;
        }

        /// <summary>
        /// Returns the index after looping around the buffer
        /// </summary>
        public int GetNormalizedIndex(int absoluteIndex) {
            if (startIndex == -1 || absoluteIndex <= startIndex) return -1;
            return (absoluteIndex - startIndex) % SegCount;
        }

        /// <summary>
        /// Clears the buffer at the specified local index
        /// </summary>
        /// <param name="index"></param>
        public bool Clear(int index) {
            if (index < 0) return false;

            // If the index is out of bounds, then we
            // loop that around and use the local index
            if (index >= SegCount)
                index = GetNormalizedIndex(index);
            AudioClip.SetData(new float[SegDataLen], index * SegDataLen);
            return true;
        }

        /// <summary>
        /// Clear the entire buffer
        /// </summary>
        public void Clear() {
            AudioClip.SetData(new float[SegDataLen * SegCount], 0);
        }
    }
}
