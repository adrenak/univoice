using System.Collections.Generic;

namespace Adrenak.UniVoice {
    public class VoiceSettings {
        /// <summary>
        /// If true, we don't want to listen to any peer
        /// </summary>
        public bool muteAll;

        /// <summary>
        /// The peers we don't want to listen to
        /// </summary>
        public List<int> mutedPeers = new List<int>();

        /// <summary>
        /// If true, we don't want any peer to listen to us
        /// </summary>
        public bool deafenAll;

        /// <summary>
        /// The peers we don't want listening to us
        /// </summary>
        public List<int> deafenedPeers = new List<int>();

        /// <summary>
        /// The tags associated with this client.
        /// DO NOT USE COMMAS (,)
        /// </summary>
        public List<string> myTags = new List<string>();

        /// <summary>
        /// The tags, which if associated with a peer, would cause
        /// those the audio of that peer to not be send to this peer
        /// DO NOT USE COMMAS (,)
        /// </summary>
        public List<string> mutedTags = new List<string>();

        /// <summary>
        /// The tags, which if associated with a peer, would cause
        /// those peers to not receive audio from this client
        /// DO NOT USE COMMAS (,)
        /// </summary>
        public List<string> deafenedTags = new List<string>();

        /// <summary>
        /// Sets the deaf status of a peer
        /// </summary>
        public void SetDeaf(int peerId, bool state) {
            if(state) {
                if (!deafenedPeers.Contains(peerId))
                    deafenedPeers.Add(peerId);
            }
            else {
                if (deafenedPeers.Contains(peerId))
                    deafenedPeers.Remove(peerId);
            }
        }

        /// <summary>
        /// Sets the mute status of a peer
        /// </summary>
        public void SetMute(int peerId, bool state) {
            if (state) {
                if (!mutedPeers.Contains(peerId))
                    mutedPeers.Add(peerId);
            }
            else {
                if (mutedPeers.Contains(peerId))
                    mutedPeers.Remove(peerId);
            }
        }
    }
}