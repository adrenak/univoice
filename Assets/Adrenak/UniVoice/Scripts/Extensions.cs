using System;
using UnityEngine;

namespace Adrenak.UniVoice {
	public static class Extensions {
		/// <summary>
		/// Returns the position on of the AudioSource on the AudioClip from 0 to 1.
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		public static float Position(this AudioSource source) {
			return (float)source.timeSamples / source.clip.samples;
		}
	}
}
