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
        event Action OnClosedChatroom;

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
        /// This action also MUST be called for all previously
        /// existing peers when we connect to a network.
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
        event Action<short, ChatroomAudioSegment> OnAudioReceived;

        /// <summary>
        /// Fired when the local user sets audio data to a peer.
        /// </summary>
        event Action<short, ChatroomAudioSegment> OnAudioSent;
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
        #endregion

        // ====================================================================
        #region METHODS
        // ====================================================================
        /// <summary>
        /// Creates a chatroom 
        /// </summary>
        /// <param name="data">Name of the chatroom</param>
        void HostChatroom(object data = null);

        /// <summary>
        /// Closes a chatroom that the local user is hosting
        /// </summary>
        /// <param name="data">Any arguments for closing the room</param>
        void CloseChatroom(object data = null);

        /// <summary>
        /// Joins a chatroom
        /// </summary>
        /// <param name="data">The name of the chatroom to join</param>
        void JoinChatroom(object data = null);

        /// <summary>
        /// Leaves the chatroom the local user is currently in, if any
        /// </summary>
        /// <param name="data">Any arguments for leaving the room</param>
        void LeaveChatroom(object data = null);

        /// <summary>
        /// Sends audio data over the network
        /// </summary>
        /// <param name="data">The data to be transmitted.</param>
        void SendAudioSegment(short peerID, ChatroomAudioSegment data);
        #endregion
    }
}
