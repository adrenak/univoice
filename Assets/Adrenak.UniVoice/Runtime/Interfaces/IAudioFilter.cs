namespace Adrenak.UniVoice {
    /// <summary>
    /// Offers ways to modify audio after being captured.
    /// To prevent the audio from being sent (for example when performing some pass or gating)
    /// return an empty byte array (new byte[0])
    /// </summary>
    public interface IAudioFilter {
        AudioFrame Run(AudioFrame input);
    }
}
