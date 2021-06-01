using System.Collections.Generic;

using UnityEngine;

namespace Adrenak.UniVoice {
    public static class Extensions {
        /// <summary>
        /// Returns the normalized position of the AudioSource on anAudioClip
        /// </summary>
        public static float Position(this AudioSource source) {
            return (float)source.timeSamples / source.clip.samples;
        }

        /// <summary>
        /// Adds the given key to the dictionary with a value if the key
        /// is not present before.
        /// </summary>
        /// <param name="t">The key to check and add</param>
        /// <param name="k">The value if the key is added</param>
        public static void EnsureKey<T, K>
        (this Dictionary<T, K> dict, T t, K k) {
            if (!dict.ContainsKey(t))
                dict.Add(t, k);
        }

        /// <summary>
        /// Ensures a key and value pair are in the dictionary
        /// </summary>
        /// <param name="t">The key to ensure</param>
        /// <param name="k">The value associated with the key</param>
        public static void EnsurePair<T, K>
        (this Dictionary<T, K> dict, T t, K k) {
            if (dict.ContainsKey(t))
                dict[t] = k;
            else
                dict.Add(t, k);
        }
    }
}
