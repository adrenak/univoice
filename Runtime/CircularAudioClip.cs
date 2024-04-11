using System.Collections.Generic;

using UnityEngine;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Used to arrange out of order and skipped audio segments for better playback.
    /// </summary>
    public class CircularAudioClip {
        public AudioClip AudioClip { get; private set; }
        public int Size { get; private set; }
        public int SegmentSamplesCount { get; private set; }

        public int ReadIndex { get; private set; }
        public int WriteIndex { get; private set; }

        readonly Dictionary<int, long> indexToTimestamp = new Dictionary<int, long>();
        readonly Dictionary<long, int> timestampToIndex = new Dictionary<long, int>();

        /// <summary>
        /// Creates an instance using an <see cref="IAudioInput"/>
        /// </summary>
        /// <param name="input">The <see cref="IAudioInput"/> to use</param>
        /// <param name="size">The number of audio segments to hold</param>
        /// <param name="clipName">The name of the AudioClip created internally</param>
        public CircularAudioClip(IAudioInput input, int size = 10, string clipName = null)
        : this(input.Frequency, input.ChannelCount, input.SegmentSamplesLength(), size, clipName) {
        
        }

        /// <summary>
        /// Create an instance
        /// </summary>
        /// <param name="frequency">The frequency of the audio</param>
        /// <param name="channels">Number of channels in the audio</param>
        /// <param name="segmentSamplesCount">Number of samples in a single segment</param>
        /// <param name="size">Number of segments stored in buffer </param>
        public CircularAudioClip(
            int frequency,
            int channels,
            int segmentSamplesCount,
            int size = 10,
            string clipName = null
        ) {
            SegmentSamplesCount = segmentSamplesCount;
            Size = size;

            AudioClip = AudioClip.Create(
                clipName ?? "clip",
                segmentSamplesCount * size,
                channels,
                frequency,
                false
            );
        }

        public bool Write(long timestamp, float[] samples) {
            if (samples.Length != SegmentSamplesCount) return false;

            // If this is the first samples array we receive, we just 
            // set the data to the AudioClip with a zero offset
            if (WriteIndex == 0 && ReadIndex == 0) {
                WriteInternal(timestamp, samples);
            }
            else {
                // We find out the timestamp at which the last read
                // sample was prepared at. If it was later than what
                // we are wrying to write, we ignore it. 
                if(indexToTimestamp.ContainsKey(ReadIndex)) {
                    var currSampleTimestamp = indexToTimestamp[ReadIndex];
                    if (timestamp < currSampleTimestamp) return false;
                }

                WriteInternal(timestamp, samples);
            }

            return true;
        }

        void WriteInternal(long timestamp, float[] samples) {
            AudioClip.SetData(samples, WriteIndex * SegmentSamplesCount);
            indexToTimestamp.EnsurePair(WriteIndex, timestamp);
            timestampToIndex.EnsurePair(timestamp, WriteIndex);
            WriteIndex = (WriteIndex + 1) % Size;
        }

        /// <summary>
        /// To be called every frame with AudioSource.timeSamples
        /// </summary>
        /// <param name="timeSamples"></param>
        public void UpdateReadIndex(int timeSamples) {
            if (timeSamples == 0) 
                ReadIndex = 0;
            else
                ReadIndex = timeSamples / (SegmentSamplesCount * 1);
        }
    }
}