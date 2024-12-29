using System;
using System.Collections.Generic;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Audio server interface. 
    /// The implementation of this class is generally based on some networking 
    /// framework such as Mirror, FishNet, Unity Netcode, etc.
    /// </summary>
    /// <typeparam name="T">
    /// The identifier data type used by the framework you're using in your implementation.
    /// For example, Mirror identifies players using int. So, a MirrorAudioClient class
    /// that implements this interface would be MirrorAudioServer : IAudioServer<int>
    /// </typeparam>
    public interface IAudioServer<T> : IDisposable {
        /// <summary>
        /// Event fired when the server starts
        /// </summary>
        event Action OnServerStart;

        /// <summary>
        /// Event fired when the server stops
        /// </summary>
        event Action OnServerStop;

        /// <summary>
        /// Event fired when the peer voice settings are updated
        /// </summary>
        event Action OnClientVoiceSettingsUpdated;

        /// <summary>
        /// IDs of all the clients in the voice chat
        /// </summary>
        List<T> ClientIDs { get; }

        /// <summary>
        /// <see cref="VoiceSettings"/> of every client connected to the server
        /// </summary>
        Dictionary<T, VoiceSettings> ClientVoiceSettings { get; }
    }
}
