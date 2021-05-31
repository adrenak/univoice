using System;
using System.Collections.Generic;

namespace Adrenak.UniVoice {
    /// <summary>
    /// A chatroom specific networking interface for creating and
    /// joining chatrooms as well as sending and receiving data to
    /// and from chatroom peers. Provides events for relevant things.
    /// </summary>
    public interface IChatroomNetwork : IDisposable {       
        /// <summary>
        /// Fired when a chatroom is created
        /// </summary>
        event Action OnChatroomCreated;

        /// <summary>
        /// Fired when the attempt to create a chatroom fails. Provides an exception.
        /// </summary>
        event Action<Exception> OnChatroomCreationFailed;
        
        /// <summary>
        /// Fired when a chatroom is closed.
        /// </summary>
        event Action OnChatroomClosed;

        /// <summary>
        /// Fired when the local user joins a chatroom. Provides the chatroom ID assigned.
        /// </summary>
        event Action<short> OnJoined;

        /// <summary>
        /// Fired when an attempt to join a chatroom fails. Provides an exception.
        /// </summary>
        event Action<Exception> OnJoiningFailed;

        /// <summary>
        /// Fired when the local user leaves a chatroom
        /// </summary>
        event Action OnLeft;

        /// <summary>
        /// Fired when a peer (another user in the chatroom) joins the chatroom. Provides the ID of the peer.
        /// </summary>
        event Action<short> OnPeerJoined;

        /// <summary>
        /// Fired when a peer (another user in the chatroom) leaves the chatroom. Provides the ID of the peer.
        /// </summary>
        event Action<short> OnPeerLeft;

        /// <summary>
        /// Fired when the network receives audio data from a peer. 
        /// </summary>
        event Action<ChatroomAudioDTO> OnAudioReceived;

        /// <summary>
        /// Fired when the local user sets audio data to a peer.
        /// </summary>
        event Action<ChatroomAudioDTO> OnAudioSent;

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

        /// <summary>
        /// Creates a chatroom 
        /// </summary>
        /// <param name="chatroomName">Name of the chatroom to be created</param>
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
    }
}
