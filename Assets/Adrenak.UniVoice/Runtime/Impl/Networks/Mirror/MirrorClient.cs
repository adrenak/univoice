#if UNIVOICE_MIRROR_NETWORK
using System;
using System.Linq;
using System.Collections.Generic;

using Mirror;
using Adrenak.BRW;
using UnityEngine;

namespace Adrenak.UniVoice.Networks {
    /// <summary>
    /// Activate this class by including the UNIVOICE_MIRROR_NETWORK compilaton symbol
    /// in your project.
    /// This is the implementation of <see cref="IAudioClient{T}"/> interface for Mirror.
    /// It uses the Mirror transport to send and receive UniVoice data to the server.
    /// </summary>
    public class MirrorClient : IAudioClient<int> {
        const string TAG = "[MirrorClient]";

        public int ID { get; private set; } = -1;

        public List<int> PeerIDs { get; private set; }

        public VoiceSettings YourVoiceSettings { get; private set; }

        public event Action<int, List<int>> OnJoined;
        public event Action OnLeft;
        public event Action<int> OnPeerJoined;
        public event Action<int> OnPeerLeft;
        public event Action<int, AudioFrame> OnReceivedPeerAudioFrame;

        public MirrorClient() {
            PeerIDs = new List<int>();
            YourVoiceSettings = new VoiceSettings();

            NetworkManager.singleton.transport.OnClientConnected += OnClientConnected;
            NetworkManager.singleton.transport.OnClientDisconnected += OnClientDisconnected;
            NetworkClient.RegisterHandler<MirrorMessage>(OnReceivedMessage, false);
        }

        public void Dispose() {
            NetworkManager.singleton.transport.OnClientConnected -= OnClientConnected;
            NetworkManager.singleton.transport.OnClientDisconnected -= OnClientDisconnected;
            NetworkClient.UnregisterHandler<MirrorMessage>();
        }

        void OnClientConnected() {
            NetworkClient.ReplaceHandler<MirrorMessage>(OnReceivedMessage);
        }

        void OnClientDisconnected() {
            NetworkClient.ReplaceHandler<MirrorMessage>(OnReceivedMessage);
            YourVoiceSettings = new VoiceSettings();
            var oldPeerIds = PeerIDs;
            PeerIDs.Clear();
            ID = -1;
            foreach (var peerId in oldPeerIds)
                OnPeerLeft?.Invoke(peerId);
            OnLeft?.Invoke();
        }


        void OnReceivedMessage(MirrorMessage msg) {
            var reader = new BytesReader(msg.data);
            var tag = reader.ReadString();
            switch (tag) {
                // When the server sends the data to initial this client with.
                // This includes the ID of this client along with the IDs of all the
                // peers that are already connected to the server
                case MirrorMessageTags.PEER_INIT:
                    ID = reader.ReadInt();
                    Debug.unityLogger.Log(LogType.Log, TAG,
                        $"Initialized with ID {ID}");
                    OnJoined?.Invoke(ID, PeerIDs);
                    PeerIDs = reader.ReadIntArray().ToList();
                    if(PeerIDs.Count > 0)
                        Debug.unityLogger.Log(LogType.Log, TAG,
                            $"Peers {string.Join(", ", PeerIDs)}");
                    foreach (var peerId in PeerIDs)
                        OnPeerJoined?.Invoke(peerId);
                    break;

                // When the server notifies that a new peer has joined the network
                case MirrorMessageTags.PEER_JOINED:
                    var newPeerID = reader.ReadInt();
                    if (!PeerIDs.Contains(newPeerID)) {
                        Debug.unityLogger.Log(LogType.Log, TAG,
                            $"Peer {newPeerID} has joined");
                        PeerIDs.Add(newPeerID);
                        OnPeerJoined?.Invoke(newPeerID);
                    }
                    break;

                // When the server notifies that a peer has left the network
                case MirrorMessageTags.PEER_LEFT:
                    var leftPeerID = reader.ReadInt();
                    if (PeerIDs.Contains(leftPeerID)) {
                        Debug.unityLogger.Log(LogType.Log, TAG,
                            $"Peer {leftPeerID} has left");
                        PeerIDs.Remove(leftPeerID);
                        OnPeerLeft?.Invoke(leftPeerID);
                    }
                    break;

                // When the server sends audio from a peer meant for this client
                case MirrorMessageTags.AUDIO_FRAME:
                    var sender = reader.ReadInt();
                    if (sender == ID || !PeerIDs.Contains(sender))
                        return;
                    var frame = new AudioFrame {
                        timestamp = reader.ReadLong(),
                        frequency = reader.ReadInt(),
                        channelCount = reader.ReadInt(),
                        samples = reader.ReadByteArray()
                    };
                    OnReceivedPeerAudioFrame?.Invoke(sender, frame);
                    break;
            }
        }

        /// <summary>
        /// Sends an audio frame captured on this client to the server
        /// </summary>
        /// <param name="frame"></param>
        public void SendAudioFrame(AudioFrame frame) {
            if (ID == -1)
                return;
            var writer = new BytesWriter();
            writer.WriteString(MirrorMessageTags.AUDIO_FRAME);
            writer.WriteInt(ID);
            writer.WriteLong(frame.timestamp);
            writer.WriteInt(frame.frequency);
            writer.WriteInt(frame.channelCount);
            writer.WriteByteArray(frame.samples);

            var message = new MirrorMessage {
                data = writer.Bytes
            };
            NetworkClient.Send(message, Channels.Unreliable);
        }

        /// <summary>
        /// Updates the server with the voice settings of this client
        /// </summary>
        public void SubmitVoiceSettings() {
            if (ID == -1)
                return;
            var writer = new BytesWriter();
            writer.WriteString(MirrorMessageTags.VOICE_SETTINGS);
            writer.WriteInt(YourVoiceSettings.muteAll ? 1 : 0);
            writer.WriteIntArray(YourVoiceSettings.mutedPeers.ToArray());
            writer.WriteInt(YourVoiceSettings.deafenAll ? 1 : 0);
            writer.WriteIntArray(YourVoiceSettings.deafenedPeers.ToArray());
            var message = new MirrorMessage {
                data = writer.Bytes
            };
            NetworkClient.Send(message, Channels.Reliable);
        }
    }
}
#endif