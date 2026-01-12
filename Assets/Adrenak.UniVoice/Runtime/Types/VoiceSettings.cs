using System.Collections.Generic;
using System.Text;

namespace Adrenak.UniVoice {
    public class VoiceSettings {
        #region GLOBAL
        /// <summary>
        /// If true, we don't want to listen to any peer
        /// </summary>
        public bool muteAll;

        /// <summary>
        /// If true, we don't want any peer to listen to us
        /// </summary>
        public bool deafenAll;
        #endregion

        #region ID BASED
        /// <summary>
        /// The peers we don't want to listen to
        /// </summary>
        public List<int> mutedPeers = new List<int>();

        /// <summary>
        /// The peers we don't want listening to us
        /// </summary>
        public List<int> deafenedPeers = new List<int>();

        /// <summary>
        /// Sets the deaf status of a peer
        /// </summary>
        public void SetDeaf(int peerId, bool state) {
            if (state) {
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
        #endregion

        #region TAG BASED
        private List<string> _myTags = new List<string>();
        private List<string> _mutedTags = new List<string>();
        private List<string> _deafenedTags = new List<string>();

        /// <summary>
        /// List of tags associated with this client.
        /// Use <see cref="AddMyTag"/> and <see cref="RemoveMyTag"/>
        /// to add or remove tags.
        /// </summary>
        public List<string> myTags { get { return _myTags; } set { _myTags = value; } }

        /// <summary>
        /// List of tags muted by this client.
        /// Use <see cref="AddMutedTag"/> and <see cref="RemoveMutedTag"/>
        /// to add or remove tags.
        /// </summary>
        public List<string> mutedTags { get { return _mutedTags; } set { _mutedTags = value; } }

        /// <summary>
        /// List of tags deafened by this client.
        /// Use <see cref="AddDeafenedTag"/> and <see cref="RemoveDeafenedTag"/>
        /// to add or remove tags.
        /// </summary>
        public List<string> deafenedTags { get { return _deafenedTags; } set { _deafenedTags = value; } }

        private bool IsTagValid(string tag) {
            if (string.IsNullOrEmpty(tag) || tag.Contains(","))
                return false;
            return true;
        }

        /// <summary>
        /// Adds/Removes a tag from myTags list
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="enable"></param>
        /// <returns></returns>
        public bool SetMyTag(string tag, bool enable) {
            if (enable)
                return AddMyTag(tag);
            else
                return RemoveMyTag(tag);
        }

        /// <summary>
        /// Adds a tag to the myTags list. Commas (',') and null/empty values are not allowed!
        /// </summary>
        /// <param name="tag">The tag to add.</param>
        /// <returns>
        /// TRUE: If the tag was accepted and added.
        /// FALSE: If tag wasn't added because the tag is not allowed or was already in the myTags list
        /// </returns>
        public bool AddMyTag(string tag) {
            if (!IsTagValid(tag))
                return false;

            if (!_myTags.Contains(tag)) {
                _myTags.Add(tag);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes a tag from the myTags list.
        /// </summary>
        /// <param name="tag">The tag to be removed.</param>
        /// <returns>
        /// TRUE: If the tag was removed.
        /// FALSE: If the tag was not removed because it's not in the myTags list
        /// </returns>
        public bool RemoveMyTag(string tag) {
            if (!_myTags.Contains(tag)) {
                return false;
            }

            _myTags.Remove(tag);
            return false;
        }

        /// <summary>
        /// Adds/Removes a tag from mutedTags list
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="enable"></param>
        /// <returns></returns>
        public bool SetMutedTag(string tag, bool enable) {
            if (enable)
                return AddMutedTag(tag);
            else
                return RemoveMutedTag(tag);
        }

        /// <summary>
        /// Adds a tag to the mutedTags list. Commas (',') and null/empty values are not allowed!
        /// </summary>
        /// <param name="tag">The tag to add.</param>
        /// <returns>
        /// TRUE: If the tag was accepted and added.
        /// FALSE: If tag wasn't added because the tag is not allowed or was already in the mutedTags list
        /// </returns>
        public bool AddMutedTag(string tag) {
            if (!IsTagValid(tag))
                return false;

            if (!_mutedTags.Contains(tag)) {
                _mutedTags.Add(tag);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes a tag from the mutedTags list.
        /// </summary>
        /// <param name="tag">The tag to be removed.</param>
        /// <returns>
        /// TRUE: If the tag was removed.
        /// FALSE: If the tag was not removed because it's not in the mutedTags list
        /// </returns>
        public bool RemoveMutedTag(string tag) {
            if (!_mutedTags.Contains(tag)) {
                return false;
            }

            _mutedTags.Remove(tag);
            return false;
        }

        /// <summary>
        /// Adds/Removes a tag from deafenedTags list
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="enable"></param>
        /// <returns></returns>
        public bool SetDeafenedTag(string tag, bool enable) {
            if (enable)
                return AddDeafenedTag(tag);
            else
                return RemoveDeafenedTag(tag);
        }

        /// <summary>
        /// Adds a tag to the deafenedTags list. Commas (',') and null/empty values are not allowed!
        /// </summary>
        /// <param name="tag">The tag to add.</param>
        /// <returns>
        /// TRUE: If the tag was accepted and added.
        /// FALSE: If tag wasn't added because the tag is not allowed or was already in the deafenedTags list
        /// </returns>
        public bool AddDeafenedTag(string tag) {
            if (!IsTagValid(tag))
                return false;

            if (!_deafenedTags.Contains(tag)) {
                _deafenedTags.Add(tag);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes a tag from the deafenedTags list.
        /// </summary>
        /// <param name="tag">The tag to be removed.</param>
        /// <returns>
        /// TRUE: If the tag was removed.
        /// FALSE: If the tag was not removed because it's not in the deafenedTags list
        /// </returns>
        public bool RemoveDeafenedTag(string tag) {
            if (!_deafenedTags.Contains(tag)) {
                return false;
            }

            _deafenedTags.Remove(tag);
            return false;
        }
        #endregion


        public override string ToString() {
            return new StringBuilder()
                .Append("muteAll: ").Append(muteAll).Append("\n")
                .Append("deafenAll: ").Append(deafenAll).Append("\n")
                .Append("mutedPeers: ").Append(string.Join(", ", mutedPeers)).Append("\n")
                .Append("deafenedPeers: ").Append(string.Join(", ", deafenedPeers)).Append("\n")
                .Append("myTags: ").Append(string.Join(", ", myTags)).Append("\n")
                .Append("mutedTags: ").Append(string.Join(", ", mutedTags)).Append("\n")
                .Append("deafenedTags: ").Append(string.Join(", ", deafenedTags))
                .ToString();
        }
    }
}