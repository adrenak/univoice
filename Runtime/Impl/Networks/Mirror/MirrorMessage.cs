#if UNIVOICE_MIRROR_NETWORK || UNIVOICE_NETWORK_MIRROR
using System;

using Mirror;

namespace Adrenak.UniVoice.Networks {
    /// <summary>
    /// The messages exchanged between the server and client. 
    /// To see how the Mirror implementation of UniVoice uses this struct
    /// find the references to the <see cref="data"/> object in the project.
    /// The gist is, it uses BRW (https://www.github.com/adrenak/brw) to 
    /// write and read data. The data always starts with a tag. All the tags
    /// used for this UniVoice Mirror implementation are available in 
    /// <see cref="MirrorMessageTags"/>
    /// </summary>
    [Serializable]
    public struct MirrorMessage : NetworkMessage {
        public byte[] data;
    }
}
#endif