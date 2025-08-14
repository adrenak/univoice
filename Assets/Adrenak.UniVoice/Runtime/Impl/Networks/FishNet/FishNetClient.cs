#if FISHNET
using System;
using System.Collections.Generic;
using System.Linq;
using Adrenak.BRW;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// This is the implementation of <see cref="IAudioClient{T}"/> interface for FishNet.
    /// It uses the FishNet to send and receive UniVoice data to the server.
    /// </summary>
    public class FishNetClient : IAudioClient<int>
    {
        private const string TAG = "[FishNetClient]";
        public int ID { get; private set; } = -1;

        public List<int> PeerIDs { get; private set; }
        public VoiceSettings YourVoiceSettings { get; private set; }

        public event Action<int, List<int>> OnJoined;
        public event Action OnLeft;
        public event Action<int> OnPeerJoined;
        public event Action<int> OnPeerLeft;
        public event Action<int, AudioFrame> OnReceivedPeerAudioFrame;

        private NetworkManager _networkManager;

        public FishNetClient()
        {
            PeerIDs = new List<int>();
            YourVoiceSettings = new VoiceSettings();
            
            _networkManager = InstanceFinder.NetworkManager;
            _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionStateChanged;
            _networkManager.ClientManager.OnAuthenticated += OnClientAuthenticated;
            _networkManager.ClientManager.OnRemoteConnectionState += OnRemoteConnectionStateChanged;
            _networkManager.ClientManager.RegisterBroadcast<FishNetBroadcast>(OnReceivedMessage);
        }
        
        public void Dispose()
        {
            if (_networkManager)
            {
                _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionStateChanged;
                _networkManager.ClientManager.OnAuthenticated -= OnClientAuthenticated;
                _networkManager.ClientManager.OnRemoteConnectionState -= OnRemoteConnectionStateChanged;
                _networkManager.ClientManager.UnregisterBroadcast<FishNetBroadcast>(OnReceivedMessage);
            }
            PeerIDs.Clear();
        }
        
        private void OnRemoteConnectionStateChanged(RemoteConnectionStateArgs args)
        {
            // Don't process connection state changes before the client is authenticated
            if (_networkManager.ClientManager.Connection.ClientId < 0)
                return;
            
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                var newPeerID = args.ConnectionId;
                if (!PeerIDs.Contains(newPeerID))
                {
                    PeerIDs.Add(newPeerID);
                    Debug.unityLogger.Log(LogType.Log, TAG,
                        $"Peer {newPeerID} joined. Peer list is now {string.Join(", ", PeerIDs)}");
                    OnPeerJoined?.Invoke(newPeerID);
                }
            }
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                var leftPeerID = args.ConnectionId;
                if (PeerIDs.Contains(leftPeerID))
                {
                    PeerIDs.Remove(leftPeerID);
                    var log2 = $"Peer {leftPeerID} left. ";
                    if (PeerIDs.Count == 0)
                        log2 += "There are no peers anymore.";
                    else
                        log2 += $"Peer list is now {string.Join(", ", PeerIDs)}";

                    Debug.unityLogger.Log(LogType.Log, TAG, log2);
                    OnPeerLeft?.Invoke(leftPeerID);
                }
            }
        }
        
        private void OnClientAuthenticated()
        {
            // We need to use OnClientAuthenticated to ensure the client does have ClientId set
            ID = _networkManager.ClientManager.Connection.ClientId;
            PeerIDs = _networkManager.ClientManager.Clients.Keys.Where(x => x != ID).ToList();
            
            var log = $"Initialized with ID {ID}. ";
            if (PeerIDs.Count > 0)
                log += $"Peer list: {string.Join(", ", PeerIDs)}";
            else
                log += "There are currently no peers.";
            Debug.unityLogger.Log(LogType.Log, TAG, log);
            
            OnJoined?.Invoke(ID, PeerIDs);
            foreach (var peerId in PeerIDs)
                OnPeerJoined?.Invoke(peerId);
        }
        
        private void OnClientConnectionStateChanged(ClientConnectionStateArgs args)
        {
            // We check only for the stopped state here, as the started state is handled in OnClientAuthenticated
            if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                YourVoiceSettings = new VoiceSettings();
                var oldPeerIds = PeerIDs.ToList();
                PeerIDs.Clear();
                ID = -1;
                foreach (var peerId in oldPeerIds)
                    OnPeerLeft?.Invoke(peerId);
                OnLeft?.Invoke();
            }
        }

        private void OnReceivedMessage(FishNetBroadcast msg, Channel channel)
        {
            var reader = new BytesReader(msg.data);
            var tag = reader.ReadString();
            switch (tag)
            {
                // When the server sends audio from a peer meant for this client
                case FishNetBroadcastTags.AUDIO_FRAME:
                    var sender = reader.ReadInt();
                    if (sender == ID || !PeerIDs.Contains(sender))
                        return;
                    var frame = new AudioFrame
                    {
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
        public void SendAudioFrame(AudioFrame frame)
        {
            if (ID == -1)
                return;
            var writer = new BytesWriter();
            writer.WriteString(FishNetBroadcastTags.AUDIO_FRAME);
            writer.WriteInt(ID);
            writer.WriteLong(frame.timestamp);
            writer.WriteInt(frame.frequency);
            writer.WriteInt(frame.channelCount);
            writer.WriteByteArray(frame.samples);

            var message = new FishNetBroadcast
            {
                data = writer.Bytes
            };

            if (_networkManager.ClientManager.Started) 
                _networkManager.ClientManager.Broadcast(message, Channel.Unreliable);
        }

        /// <summary>
        /// Updates the server with the voice settings of this client
        /// </summary>
        public void SubmitVoiceSettings() 
        {
            if (ID == -1)
                return;
            var writer = new BytesWriter();
            writer.WriteString(FishNetBroadcastTags.VOICE_SETTINGS);
            writer.WriteInt(YourVoiceSettings.muteAll ? 1 : 0);
            writer.WriteIntArray(YourVoiceSettings.mutedPeers.ToArray());
            writer.WriteInt(YourVoiceSettings.deafenAll ? 1 : 0);
            writer.WriteIntArray(YourVoiceSettings.deafenedPeers.ToArray());
            writer.WriteString(string.Join(",", YourVoiceSettings.myTags));
            writer.WriteString(string.Join(",", YourVoiceSettings.mutedTags));
            writer.WriteString(string.Join(",", YourVoiceSettings.deafenedTags));

            var message = new FishNetBroadcast() {
                data = writer.Bytes
            };
            _networkManager.ClientManager.Broadcast(message);
        }
    }
}
#endif
