#if MIRROR
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

        readonly MirrorModeObserver mirrorEvents;

        public MirrorClient() {
            PeerIDs = new List<int>();
            YourVoiceSettings = new VoiceSettings();

            mirrorEvents = MirrorModeObserver.New("for MirrorClient");
            mirrorEvents.ModeChanged += OnModeChanged;

            NetworkClient.RegisterHandler<MirrorMessage>(OnReceivedMessage, false);
        }

        public void Dispose() {
            PeerIDs.Clear();
        }

        void OnModeChanged(NetworkManagerMode oldMode, NetworkManagerMode newMode) {
            // For some reason, handlers don't always work as expected when the connection mode changes
            NetworkClient.ReplaceHandler<MirrorMessage>(OnReceivedMessage);

            bool clientOnlyToOffline = newMode == NetworkManagerMode.Offline && oldMode == NetworkManagerMode.ClientOnly;
            bool hostToServerOnlyOrOffline = oldMode == NetworkManagerMode.Host;

            if (clientOnlyToOffline || hostToServerOnlyOrOffline) {
                // We unregister the handler only when the device was a client.
                // If it was a Host that's now a ServerOnly, we still need the handler as it's used in MirrorServer
                if (clientOnlyToOffline)
                    NetworkClient.UnregisterHandler<MirrorMessage>();
                
                OnClientDisconnected();
            }
        }

        void OnClientDisconnected() {
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
                    PeerIDs = reader.ReadIntArray().ToList();

                    string log = $"Initialized with ID {ID}. ";
                    if (PeerIDs.Count > 0)
                        log += $"Peer list: {string.Join(", ", PeerIDs)}";
                    else
                        log += "There are currently no peers.";
                    Debug.unityLogger.Log(LogType.Log, TAG, log);

                    OnJoined?.Invoke(ID, PeerIDs);
                    foreach (var peerId in PeerIDs)
                        OnPeerJoined?.Invoke(peerId);
                    break;

                // When the server notifies that a new peer has joined the network
                case MirrorMessageTags.PEER_JOINED:
                    var newPeerID = reader.ReadInt();
                    if (!PeerIDs.Contains(newPeerID)) {
                        PeerIDs.Add(newPeerID);
                        Debug.unityLogger.Log(LogType.Log, TAG,
                            $"Peer {newPeerID} joined. Peer list is now {string.Join(", ", PeerIDs)}");
                        OnPeerJoined?.Invoke(newPeerID);
                    }
                    break;

                // When the server notifies that a peer has left the network
                case MirrorMessageTags.PEER_LEFT:
                    var leftPeerID = reader.ReadInt();
                    if (PeerIDs.Contains(leftPeerID)) {
                        PeerIDs.Remove(leftPeerID);
                        string log2 = $"Peer {leftPeerID} left. ";
                        if (PeerIDs.Count == 0)
                            log2 += "There are no peers anymore.";
                        else
                            log2 += $"Peer list is now {string.Join(", ", PeerIDs)}";

                        Debug.unityLogger.Log(LogType.Log, TAG, log2);
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
            writer.WriteString(string.Join(",", YourVoiceSettings.myTags));
            writer.WriteString(string.Join(",", YourVoiceSettings.mutedTags));
            writer.WriteString(string.Join(",", YourVoiceSettings.deafenedTags));

            var message = new MirrorMessage {
                data = writer.Bytes
            };
            NetworkClient.Send(message, Channels.Reliable);
        }
    }
}
#endif