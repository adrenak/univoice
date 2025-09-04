#if MIRROR
using Mirror;

using System;

using UnityEngine;

namespace Adrenak.UniVoice.Networks {
    /// <summary>
    /// Observes the mode of the Mirror NetworkManager and fires an event
    /// when it changes
    /// </summary>
    public class MirrorModeObserver : MonoBehaviour {
        const string TAG = "[MirrorModeObserver]";

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
        public static MirrorModeObserver New(string name = "") {
            var go = new GameObject($"MirrorEventProvider {name}");
            DontDestroyOnLoad(go);
            return go.AddComponent<MirrorModeObserver>();
        }

        void Update() {
            var newMode = NetworkManager.singleton.mode;
            if (lastMode != newMode) {
                try {
                    ModeChanged?.Invoke(lastMode, newMode);
                }
                catch (Exception e) {
                    Debug.unityLogger.Log(LogType.Error, TAG, "Exception while handling Mirror Mode change: " + e);
                }
                lastMode = newMode;
            }
        }
    }
}
#endif