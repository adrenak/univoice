#if UNIVOICE_MIRROR_NETWORK || UNIVOICE_NETWORK_MIRROR
using Mirror;

using System;

using UnityEngine;

namespace Adrenak.UniVoice.Networks {
    /// <summary>
    /// Observes the mode of the Mirror NetworkManager and fires an event
    /// when it changes
    /// </summary>
    public class MirrorModeObserver : MonoBehaviour {
        /// <summary>
        /// Event fired when the Mirror NetworkManager changes the mode
        /// </summary>
        public event Action<NetworkManagerMode, NetworkManagerMode> ModeChanged;

        [Obsolete("Use .New instead.", true)]
        public MirrorModeObserver() { }

        NetworkManagerMode lastMode = NetworkManagerMode.Offline;

        /// <summary>
        /// Creates a new instance of this class on a GameObject
        /// </summary>
        /// <returns></returns>
        public static MirrorModeObserver New() {
            var go = new GameObject("MirrorEventProvider");
            DontDestroyOnLoad(go);
            return go.AddComponent<MirrorModeObserver>();
        }

        void Update() {
            var newMode = NetworkManager.singleton.mode;
            if (lastMode != newMode) {
                ModeChanged?.Invoke(lastMode, newMode);
                lastMode = newMode;
            }
        }
    }
}
#endif