using System;

namespace Adrenak.UniVoice {
    /// <summary>
    /// An audio input implementation that doesn't do anything.
    /// Use this when the device doesn't have any input mode. 
    /// This is especially useful when setting up the ClientSession
    /// object on a dedicated server that likely isn't going to have 
    /// and mic or other audio capture devices.
    /// </summary>
    public class EmptyAudioInput : IAudioInput {
        public int Frequency => 1;

        public int ChannelCount => 1;

        public int SegmentRate => 1;

        #pragma warning disable CS0067
        public event Action<AudioFrame> OnFrameReady;
        #pragma warning restore

        public void Dispose() { }
    }
}