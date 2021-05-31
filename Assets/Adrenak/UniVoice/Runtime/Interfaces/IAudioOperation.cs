namespace Adrenak.UniVoice {
    /// <summary>
    /// Used to apply operations on audio. Ex. Use this for your
    /// denoising algorithms or audio effects.
    /// </summary>
    public interface IAudioOperation {
        /// <summary>
        /// Executes the operation
        /// </summary>
        /// <param name="input">The audio samples sennt as input</param>
        /// <returns>The result/output of the operation</returns>
        float[] Execute(float[] input);
    }
}
