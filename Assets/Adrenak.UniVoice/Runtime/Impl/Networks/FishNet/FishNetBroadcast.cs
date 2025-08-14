#if FISHNET
using System;
using FishNet.Broadcast;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// The messages exchanged between the server and client. 
    /// To see how the Mirror implementation of UniVoice uses this struct
    /// find the references to the <see cref="data"/> object in the project.
    /// The gist is, it uses BRW (https://www.github.com/adrenak/brw) to 
    /// write and read data. The data always starts with a tag. All the tags
    /// used for this UniVoice FishNet implementation are available in 
    /// <see cref="FishNetBroadcastTags"/>
    /// </summary>
    [Serializable]
    public struct FishNetBroadcast : IBroadcast
    {
        public byte[] data;
    }
}
#endif
