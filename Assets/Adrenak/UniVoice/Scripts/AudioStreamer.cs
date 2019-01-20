using Adrenak.UniMic;
using UnityEngine;

namespace Adrenak.UniVoice {
	/// <summary>
	/// This class feeds incoming segments of audio to an AudioBuffer and plays the buffer's clip on 
	/// an AudioSource. It handles adjusting the speed of the playback based on latency fluctuations
	/// to a degree by changing the audio pitch to keep incoming stream and playback in sync.
	/// It also clears segments of the buffer based on the AudioSource's current position.
	/// NOTE: Setting the pitch of an AudioSource also changes the playback speed of the clip.
	/// </summary>
	public class AudioStreamer : MonoBehaviour {
		public AudioBuffer Buffer { get; private set; }
		public AudioSource Source { get; private set; }

		int m_Offset;

		// Prevent contruction using 'new' as this is a MonoBehaviour class
		AudioStreamer() { }

		/// <summary>
		/// Creates a new instance
		/// </summary>
		/// <param name="buffer">The AudioBuffer that the streamer operates on.</param>
		/// <param name="source">The AudioSource from where the incomming audio is played.</param>
		/// <param name="idealSegsPerSec">The number of audio segments that should be arriving every second, under ideal situations.</param>
		/// <returns></returns>
		public static AudioStreamer New(AudioBuffer buffer, AudioSource source) {
			var cted = new GameObject("AudioStreamer").AddComponent<AudioStreamer>();
			DontDestroyOnLoad(cted.gameObject);

			source.loop = true;
			source.clip = buffer.Clip;

			cted.Buffer = buffer;
			cted.Source = source;

			return cted;
		}

		int lastIndex;
		/// <summary>
		/// Runs every frame to see if the AudioSource has just moved to a new segment
		/// so that the previous one can be set to zero (silence). This is to make sure 
		/// that if a segment is missed, its previous contents won't be played again 
		/// when the clip loops.
		/// </summary>
		private void Update() {
			if (Source.clip == null) return;

			var pos = Source.Position();
			var index = (int)(pos * Buffer.SegCapacity);

			// If we have moved to a new segment, clear the last one
			if (lastIndex != index) {
				Buffer.Clear(lastIndex);
				lastIndex = index;
			}
		}

		int count;
		/// <summary>
		/// Streams an audio segment to the streamer, which feeds it to its buffer.
		/// </summary>
		/// <param name="index">The index of the segment, as reported by the mic to know 
		/// the absolute position of the segment on the limited buffer
		/// </param>
		/// <param name="segment">The float array representing the audio data for the segment</param>
		public void Stream(int index, float[] segment) {
			// Find the segment the source is currently playing at
			var playIndex = (int)(Source.Position() * Buffer.SegCapacity);
		
			// In case Source.Position() is 1
			if (playIndex == Buffer.SegCapacity) playIndex--;

			var bufferIndex = Buffer.GetLocalIndex(index + m_Offset);

			// If we are about to write to the same segment index that we are
			// reading, then we decrement the offset so that the writing happens 
			// to the previous segment index
			if(playIndex == bufferIndex) 
				m_Offset--;

			Buffer.Feed(index + m_Offset, segment);

			// As soon as we have filled the buffer, play
			// This runs only once
			if(++count == Buffer.SegCapacity - 1)
				Source.Play();
		}
	}
}
