using System;
using System.Collections.Generic;

namespace Adrenak.UniVoice {
    public interface IAudioNetwork : IDisposable {
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
        event Action<int> OnJoinedChatroom;

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
        /// NOTE: This action also MUST be called for all previously
        /// existing peers when a local user connects to a network.
        /// This allows the local user to know about the users that
        /// were in the chatroom before they joined.
        /// </summary>
        event Action<int> OnPeerJoinedChatroom;

        /// <summary>
        /// Fired when a peer leaves the chatroom. 
        /// Provides the ID of the peer as event data.
        /// </summary>
        event Action<int> OnPeerLeftChatroom;

        /// <summary>
        /// Fired when the network receives audio data from a peer. 
        /// The first argument is the ID of the user the audio came from.
        /// The second is the audio segment.
        /// </summary>
        event Action<int, AudioFrame> OnAudioReceived;

        /// <summary>
        /// Fired when the local user sets audio data to a peer.
        /// The first argument is the ID of the user the audio was sent to.
        /// The second is the audio segment.
        /// </summary>
        event Action<int, AudioFrame> OnAudioSent;
        
        /// <summary>
        /// The ID of the local user in the current chatroom 
        /// </summary>
        int OwnID { get; }

        /// <summary>
        /// IDs of all the peers in the current chatroom (excluding <see cref="OwnID"/>)
        /// </summary>
        List<int> PeerIDs { get; }
        
        /// <summary>
        /// Sends audio data over the network
        /// </summary>
        /// <param name="data">The data to be transmitted.</param>
        void SendAudioFrame(int peerID, AudioFrame data);
    }
}
