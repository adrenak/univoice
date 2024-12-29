using System;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Source of user voice input. This would usually be implemented 
    /// over a microphone to get the users voice. But it can also be used
    /// in other ways such as streaming an mp4 file from disk. It's just 
    /// an input and the source doesn't matter.
    /// </summary>
    public interface IAudioInput : IDisposable {
        event Action<AudioFrame> OnFrameReady;
    }
}
