namespace Adrenak.UniVoice {
    /// <summary>
    /// An abstract factory that creates a concrete <see cref="IAudioOutput"/> 
    /// </summary>
    public interface IAudioOutputFactory {
        /// <summary>
        /// Creates an instance of a concrete <see cref="IAudioOutput"/> class
        /// </summary>
        IAudioOutput Create();
    }
}