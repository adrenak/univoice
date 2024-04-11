namespace Adrenak.UniVoice {
    /// <summary>
    /// Offers ways to modify incoming or outgoing audio.
    /// To prevent the audio from being sent (for example when performing some pass or gating)
    /// return new byte[0]
    /// </summary>
    public interface IAudioFilter {
        byte[] Run(byte[] input);
    }
}
