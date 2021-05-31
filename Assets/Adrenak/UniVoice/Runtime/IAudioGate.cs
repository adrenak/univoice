namespace Adrenak.UniVoice {
    /// <summary>
    /// Used to implement gates on input. Ex. Use this for deciding
    /// if the volume of the input is high enough to transmit over 
    /// the network
    /// </summary>
    public interface IAudioGate {
        /// <summary>
        /// Evaluates the input
        /// </summary>
        /// <param name="input">The audio samples sent as input</param>
        /// <returns>Whether the input passes the gate or not</returns>
        bool Evaluate(float[] input);
    }
}
