namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// Tags used by the BRW-encoded payload that flows between the UniVoice
    /// client and server, regardless of the underlying networking framework.
    /// </summary>
    public static class AudioBroadcastTags
    {
        public const string AUDIO_FRAME = "AUDIO_FRAME";
        public const string VOICE_SETTINGS = "VOICE_SETTINGS";
    }
}
