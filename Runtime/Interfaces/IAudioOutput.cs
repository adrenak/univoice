using System;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Responsible for playing audio that is sent to it. 
    /// You'd normally want a <see cref="UnityEngine.AudioSource"/> 
    /// based implementation to play the audio in Unity. But this class can 
    /// be used in other ways such as streaming the received audio to a server
    /// or writing it to a local file. It's just an audio output and the 
    /// destination depends on your implementation.
    /// </summary>
    public interface IAudioOutput : IDisposable {
        /// <summary>
        /// Feeds a <see cref="AudioFrame"/> object to the audio output.
        /// </summary>
        /// <param name="segment">The audio data to be sent.</param>
        void Feed(AudioFrame segment);
    }
}
