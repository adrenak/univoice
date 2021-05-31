using UnityEngine;

namespace Adrenak.UniVoice {
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

            if (absoluteIndex < 0 || absoluteIndex < startIndex ) return false;

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
