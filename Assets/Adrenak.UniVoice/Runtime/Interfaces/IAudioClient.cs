using System;
using System.Collections.Generic;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Audio client interface. 
    /// The implementation of this class is generally based on some networking 
    /// framework such as Mirror, FishNet, Unity Netcode, etc.
    /// </summary>
    /// <typeparam name="T">
    /// The identifier data type used by the framework you're using in your implementation.
    /// For example, Mirror identifies players using int. So, a MirrorAudioClient class
    /// that implements this interface would be MirrorAudioClient : IAudioClient<int>
    /// </typeparam>
    public interface IAudioClient<T> : IDisposable {
        /// <summary>
        /// The clients peer ID in the voice chat
        /// </summary>
        T ID { get; }

        /// <summary>
        /// IDs of all the peers (except this client) in the voice chat
        /// </summary>
        List<T> PeerIDs { get; }

        /// <summary>
        /// The voice settings of this client. Call <see cref="SubmitVoiceSettings"/>
        /// after making changes to this object to submit the updates to the server.
        /// </summary>
        VoiceSettings YourVoiceSettings { get; }

        /// <summary>
        /// Fired when this client connects and joins the voice chat
        /// Includes the following parameters
        /// - own peer ID (int). This should also get assigned to <see cref="ID"/>
        /// - IDs of other peers. This should also get assigned to <see cref="PeerIDs"/>
        /// </summary>
        event Action<T, List<T>> OnJoined;

        /// <summary>
        /// Fired when this client disconnects and leaves the voice chat
        /// </summary>
        event Action OnLeft;

        /// <summary>
        /// Fired when a new peer joins the voice chat.
        /// Provides the ID of the client as event data.
        /// </summary>
        event Action<T> OnPeerJoined;

        /// <summary>
        /// Fired when a client leaves the chatroom. 
        /// Provides the ID of the client as event data.
        /// </summary>
        event Action<T> OnPeerLeft;

        /// <summary>
        /// Event fired when an audio frame is received from
        /// another peer via the server. Parameters:
        /// - peer ID: the ID of the peer that sent the audio frame
        /// - AudioFrame: the frame containing audio data
        /// </summary>
        event Action<T, AudioFrame> OnReceivedPeerAudioFrame;

        /// <summary>
        /// Sends an audio frame to the server for being
        /// broadcasted to the other peers
        /// </summary>
        /// <param name="frame">The audio frame to be sent</param>
        void SendAudioFrame(AudioFrame frame);

        /// <summary>
        /// Submits <see cref="YourVoiceSettings"/> to the server
        /// </summary>
        void SubmitVoiceSettings();
    }
}
