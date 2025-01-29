namespace Adrenak.UniVoice.Filters {
    /// <summary>
    /// Defines supported sampling frequencies for the Opus codec via Concentus.
    /// </summary>
    public enum ConcentusFrequencies : int {
        /// <summary>
        /// 8 kHz sampling frequency, typically used for narrowband audio.
        /// </summary>
        Frequency_8000 = 8000,

        /// <summary>
        /// 12 kHz sampling frequency, suitable for medium-band audio.
        /// </summary>
        Frequency_12000 = 12000,

        /// <summary>
        /// 16 kHz sampling frequency, commonly used for wideband speech.
        /// </summary>
        Frequency_16000 = 16000,

        /// <summary>
        /// 24 kHz sampling frequency, providing good quality for music and audio.
        /// </summary>
        Frequency_24000 = 24000,

        /// <summary>
        /// 48 kHz sampling frequency, offering the highest audio quality.
        /// </summary>
        Frequency_48000 = 48000,
    }
}