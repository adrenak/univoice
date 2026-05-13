#define PURRNET
#if PURRNET
using System;
using PurrNet.Packing;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// PurrNet broadcast envelope. The payload is a BRW-encoded byte stream
    /// that always begins with one of the tags in <see cref="AudioBroadcastTags"/>
    /// followed by tag-specific fields.
    /// </summary>
    [Serializable]
    public struct PurrNetBroadcast : IPackedAuto
    {
        public byte[] data;
    }
}
#endif
