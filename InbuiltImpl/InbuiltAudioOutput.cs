using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Adrenak.UniVoice.InbuiltImplementations {
    // ========================================================================
    #region OUTPUT IMPLEMENTATION
    // ========================================================================
    /// <summary>
    /// This class feeds incoming segments of audio to an AudioBuffer 
    /// and plays the buffer's clip on an AudioSource. It also clears segments
    /// of the buffer based on the AudioSource's position.
    /// </summary>
    public class InbuiltAudioOutput : MonoBehaviour, IAudioOutput {
        enum Status {
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

        public InbuiltAudioBuffer AudioBuffer { get; private set; }
        public AudioSource AudioSource { get; private set; }
        public int MinSegCount { get; private set; }

        // Prevent contruction using 'new' as this is a MonoBehaviour class
        InbuiltAudioOutput() { }

        public string ID {
            get => AudioBuffer.AudioClip.name;
            set {
                gameObject.name = "UniVoice Peer #" + value;
                AudioBuffer.AudioClip.name = "UniVoice Peer #" + value;
            }
        }

        /// <summary>
        /// Creates a new instance using the dependencies.
        /// </summary>
        /// 
        /// <param name="buffer">
        /// The AudioBuffer that the streamer operates on.
        /// </param>
        /// 
        /// <param name="source">
        /// The AudioSource from where the incomming audio is played.
        /// </param>
        /// 
        /// <param name="minSegCount">
        /// The minimum number of audio segments <see cref="AudioBuffer"/> 
        /// must have for the streamer to play the audio. This value is capped
        /// between 1 and <see cref="InbuiltAudioBuffer.SegCount"/> of the 
        /// <see cref="AudioBuffer"/> passed.
        /// Default: 0. Results in the value being set to the max possible.
        /// </param>
        public static InbuiltAudioOutput New
        (InbuiltAudioBuffer buffer, AudioSource source, int minSegCount = 0) {
            var ctd = source.gameObject.AddComponent<InbuiltAudioOutput>();
            DontDestroyOnLoad(ctd.gameObject);

            source.loop = true;
            source.clip = buffer.AudioClip;

            if (minSegCount != 0)
                ctd.MinSegCount = Mathf.Clamp(minSegCount, 1, buffer.SegCount);
            else
                ctd.MinSegCount = buffer.SegCount;
            ctd.AudioBuffer = buffer;
            ctd.AudioSource = source;

            return ctd;
        }

        int lastIndex = -1;
        /// <summary>
        /// (silence). 
        /// This is to make sure that if a segment is missed, its previous 
        /// contents won't be played again when the clip loops back.
        /// </summary>
        private void Update() {
            if (AudioSource.clip == null) return;

            var index = (int)(AudioSource.Position() * AudioBuffer.SegCount);

            // Check every frame to see if the AudioSource has 
            // just moved to a new segment in the AudioBuffer 
            if (lastIndex != index) {
                // If so, clear the audio buffer so that in case tha 
                // AudioSource loops around, the old contents are not played.
                AudioBuffer.Clear(lastIndex);

                segments.EnsureKey(lastIndex, Status.Behind);
                segments.EnsureKey(index, Status.Current);

                lastIndex = index;
            }

            // Check if the number of ready segments is sufficient for us to 
            // play the audio. Whereas if the number is 0, we must stop audio
            // and wait for the minimum ready segment count to be met again.
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
        /// Feeds incoming audio into the audio buffer.
        /// </summary>
        /// 
        /// <param name="index">
        /// The absolute index of the segment, as reported by the peer to know 
        /// the normalized position of the segment on the buffer
        /// </param>
        /// 
        /// <param name="audioSamples">The audio samples being fed</param>
        public void Feed
        (int index, int frequency, int channelCount, float[] audioSamples) {
            // If we already have this index, don't bother
            // It's been passed already without playing.
            if (segments.ContainsKey(index)) return;

            int locIdx = (int)(AudioSource.Position() * AudioBuffer.SegCount);
            locIdx = Mathf.Clamp(locIdx, 0, AudioBuffer.SegCount - 1);

            var bufferIndex = AudioBuffer.GetNormalizedIndex(index);

            // Don't write to the same segment index that we are reading
            if (locIdx == bufferIndex) return;

            // Finally write into the buffer 
            segments.Add(index, Status.Ahead);
            AudioBuffer.Write(index, audioSamples);
        }

        /// <summary>
        /// Disposes the instance by deleting the GameObject of the component.
        /// </summary>
        public void Dispose() {
            Destroy(gameObject);
        }
    }
    #endregion

    // ========================================================================
    #region OUTPUT FACTORY IMPLEMENTATION
    // ========================================================================
    /// <summary>
    /// Creates <see cref="InbuiltAudioOutput"/> instances
    /// </summary>
    public class InbuiltAudioOutputFactory : IAudioOutputFactory {
        public int BufferSegCount { get; private set; }
        public int MinSegCount{ get; private set; }

        public InbuiltAudioOutputFactory() : this(10, 5) { }

        public InbuiltAudioOutputFactory(int bufferSegCount, int minSegCount) {
            BufferSegCount = bufferSegCount;
            MinSegCount = minSegCount;
        }

        public IAudioOutput Create
        (int samplingRate, int channelCount, int segmentLength) {
            return InbuiltAudioOutput.New(
                new InbuiltAudioBuffer(
                    samplingRate, channelCount, segmentLength, BufferSegCount
                ),
                new GameObject($"UniVoice Peer").AddComponent<AudioSource>(),
                MinSegCount
            );
        }
    }
    #endregion

    // ========================================================================
    #region INTERNAL BUFFER
    // ========================================================================
    /// <summary>
    /// Used to arrange irregular, out of order 
    /// and skipped audio segments for better playback.
    /// </summary>
    public class InbuiltAudioBuffer {
        public AudioClip AudioClip { get; private set; }
        public int SegCount { get; private set; }
        public int SegDataLen { get; private set; }

        // Holds the first valid segment index received by the buffer
        int firstIndex;

        /// <summary>
        /// Create an instance
        /// </summary>
        /// <param name="frequency">The frequency of the audio</param>
        /// <param name="channels">Number of channels in the audio</param>
        /// <param name="segDataLen">Numer of samples in the audio </param>
        /// <param name="segCount">Number of segments stored in buffer </param>
        public InbuiltAudioBuffer(
            int frequency,
            int channels,
            int segDataLen,
            int segCount = 3,
            string clipName = null
        ) {
            clipName = clipName ?? "clip";
            AudioClip = AudioClip.Create(
                clipName,
                segDataLen * segCount,
                channels,
                frequency,
                false
            );

            firstIndex = -1;
            SegDataLen = segDataLen;
            SegCount = segCount;
        }

        /// <summary>
        /// Feed an audio segment to the buffer.
        /// </summary>
        /// 
        /// <param name="absoluteIndex">
        /// Absolute index of the audio segment from the source.
        /// </param>
        /// 
        /// <param name="audioSegment">Audio samples data</param>
        public bool Write(int absoluteIndex, float[] audioSegment) {
            // Reject if the segment length is wrong
            if (audioSegment.Length != SegDataLen) return false;

            if (absoluteIndex < 0 || absoluteIndex < firstIndex) return false;

            // If this is the first segment fed
            if (firstIndex == -1) firstIndex = absoluteIndex;

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
            if (firstIndex == -1 || absoluteIndex <= firstIndex) return -1;
            return (absoluteIndex - firstIndex) % SegCount;
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
        #endregion
    }
}
