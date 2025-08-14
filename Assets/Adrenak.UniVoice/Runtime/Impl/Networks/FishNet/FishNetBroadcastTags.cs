#if FISHNET
namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// The different types of messages we send over FishNet 
    /// to implement the <see cref="IAudioClient{T}"/> and <see cref="IAudioServer{T}"/>
    /// interfaces for FishNet
    /// </summary>
    public class FishNetBroadcastTags
    {
        public const string AUDIO_FRAME = "AUDIO_FRAME";
        public const string VOICE_SETTINGS = "VOICE_SETTINGS";
    }
}
#endif
