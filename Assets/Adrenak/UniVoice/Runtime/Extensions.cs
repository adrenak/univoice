using System.Collections.Generic;
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

		public static void EnsureKey<T, K>(this Dictionary<T, K> dict, T t, K k){
			if (!dict.ContainsKey(t))
				dict.Add(t, k);
        }

		public static void EnsurePair<T, K>(this Dictionary<T, K> dict, T t, K k){
			if (dict.ContainsKey(t))
				dict[t] = k;
			else
				dict.Add(t, k);
        }
	}
}
