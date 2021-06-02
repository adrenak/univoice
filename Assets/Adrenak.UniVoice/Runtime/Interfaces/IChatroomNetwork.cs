using System;
using System.Collections.Generic;

namespace Adrenak.UniVoice {
    /// <summary>
    /// A chatroom specific networking interface for creating & joining 
    /// chatrooms and sending & receiving data to and from chatroom peers. 
    /// </summary>
    public interface IChatroomNetwork : IDisposable {
        // ====================================================================
        #region EVENTS
        // ====================================================================
        /// <summary>
        /// Fired when a chatroom is created. 
        /// </summary>
        event Action OnCreatedChatroom;

        /// <summary>
        /// Fired when the attempt to create a chatroom fails. 
        /// Provides an exception as event data.
        /// </summary>
        event Action<Exception> OnChatroomCreationFailed;
        
        /// <summary>
        /// Fired when a chatroom is closed.
        /// </summary>
        event Action OnlosedChatroom;

        /// <summary>
        /// Fired when the local user joins a chatroom. 
        /// Provides the chatroom ID assigned as event data.
        /// </summary>
        event Action<short> OnJoinedChatroom;

        /// <summary>
        /// Fired when an attempt to join a chatroom fails. 
        /// Provides an exception as event data.
        /// </summary>
        event Action<Exception> OnChatroomJoinFailed;

        /// <summary>
        /// Fired when the local user leaves a chatroom
        /// </summary>
        event Action OnLeftChatroom;

        /// <summary>
        /// Fired when a peer joins the chatroom. 
        /// Provides the ID of the peer as event data.
        /// </summary>
        event Action<short> OnPeerJoinedChatroom;

        /// <summary>
        /// Fired when a peer leaves the chatroom. 
        /// Provides the ID of the peer as event data.
        /// </summary>
        event Action<short> OnPeerLeftChatroom;

        /// <summary>
        /// Fired when the network receives audio data from a peer. 
        /// </summary>
        event Action<ChatroomAudioDTO> OnAudioReceived;

        /// <summary>
        /// Fired when the local user sets audio data to a peer.
        /// </summary>
        event Action<ChatroomAudioDTO> OnAudioSent;
        #endregion

        // ====================================================================
        #region PROPERTIES
        // ====================================================================
        /// <summary>
        /// The ID of the local user in the current chatroom 
        /// </summary>
        short OwnID { get; }

        /// <summary>
        /// IDs of all the peers in the current chatroom
        /// </summary>
        List<short> PeerIDs { get; }

        /// <summary>
        /// Name of of the chatroom we are currently in
        /// </summary>
        string CurrentChatroomName { get; }
        #endregion

        // ====================================================================
        #region METHODS
        // ====================================================================
        /// <summary>
        /// Creates a chatroom 
        /// </summary>
        /// <param name="chatroomName">Name of the chatroom</param>
        void HostChatroom(string chatroomName);

        /// <summary>
        /// Closes a chatroom that the local user is hosting
        /// </summary>
        void CloseChatroom();

        /// <summary>
        /// Joins a chatroom
        /// </summary>
        /// <param name="chatroomName">The name of the chatroom to join</param>
        void JoinChatroom(string chatroomName);

        /// <summary>
        /// Leaves the chatroom the local user is currently in, if any
        /// </summary>
        void LeaveChatroom();

        /// <summary>
        /// Sends audio data over the network to a peer.
        /// </summary>
        /// <param name="data">The data to be transmitted.</param>
        void SendAudioSegment(ChatroomAudioDTO data);
        #endregion
    }
}
