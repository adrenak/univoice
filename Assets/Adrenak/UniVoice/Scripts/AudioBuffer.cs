using UnityEngine;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Used to put together sequences of out of order audio segments back into order for seamless playback
    /// </summary>
    public class AudioBuffer {
        public AudioClip Clip { get; private set; }
        public int SegCapacity { get; private set; }
        public int SegLength { get; private set; }

		/// <summary>
		/// Holds the first valid segment index received by the buffer
		/// </summary>
		int m_IniIndex;

        /// <summary>
        /// Create an instance
        /// </summary>
        /// <param name="frequency">The frequency of the audio segment</param>
        /// <param name="channels">Number of channels in the segments</param>
        /// <param name="segLen">Segment length. Rremains constant throughout the stream</param>
        /// <param name="segCapacity">The total number of segments stored as buffer. </param>
        public AudioBuffer (int frequency, int channels, int segLen, int segCapacity) {
            Clip = AudioClip.Create("clip", segLen * segCapacity, channels, frequency, false);

            m_IniIndex = -1;
            SegLength = segLen;
            SegCapacity = segCapacity;
        }

        /// <summary>
        /// Feed an audio segment to the buffer.
        /// </summary>
        /// <param name="absIndex">The absolute index of the audio segment from the Microphone.</param>
        /// <param name="segment">Float array representation of the audio</param>
        public int Feed(int absIndex, float[] segment) {
			// Reject if the segment length is wrong
            if (segment.Length != SegLength) return -1;

			// TODO: Not required?
            if (absIndex < m_IniIndex) return -1;

			// If this is the first segment fed
            if (m_IniIndex == -1) m_IniIndex = absIndex;

			// TODO: Not required?
			if (absIndex < 0) absIndex = SegCapacity - 1;

			// Convert the absolute index into a looped-around index
			var localIndex = GetLocalIndex(absIndex);

			// Set the segment at the clip data at the right index
			if(localIndex >= 0 && localIndex < SegCapacity)
				Clip.SetData(segment, localIndex * SegLength);
			return localIndex;
        }

		/// <summary>
		/// Returns the index after looping around the buffer
		/// </summary>
		public int GetLocalIndex(int absoluteIndex) {
			if (m_IniIndex == -1 || absoluteIndex <= m_IniIndex) return -1;
			return (absoluteIndex - m_IniIndex) % SegCapacity;
		}

		/// <summary>
		/// Clears the buffer at the specified local index
		/// </summary>
		/// <param name="index"></param>
		public bool Clear(int index) {
			if (index < 0) return false;

			// If the index is out of bounds, then we loop that around
			// and use the local index
			if (index >= SegCapacity)
				index = GetLocalIndex(index);
			Clip.SetData(new float[SegLength], index * SegLength);
			return true;
		}

		/// <summary>
		/// Clear the entire buffer
		/// </summary>
		public void Clear() {
			Clip.SetData(new float[SegLength * SegCapacity], 0);
		}
    }
}
