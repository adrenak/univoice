using System;
using System.Linq;
using System.Collections.Generic;

using Debug = UnityEngine.Debug;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Provides the means to host or connect to a chatroom.
    /// </summary>
    public class AudioChat : IDisposable {
        const string TAG = "AudioChat";

        // ====================================================================
        #region PROPERTIES
        // ====================================================================
        /// <summary>
        /// The underlying network which the agent uses to host or connect to 
        /// chatrooms, and send and receive data to and from peers
        /// </summary>
        public IAudioNetwork Network { get; private set; }

        /// <summary>
        /// Source of captured audio that can be transmitted over the network to peers
        /// </summary>
        public IAudioInput Input { get; private set; }

        /// <summary>
        /// The filters to run on captured audio before it is sent to the network.
        /// Filters are run in the order they are placed in this list.
        /// </summary>
        public List<IAudioFilter> InputFilters = new List<IAudioFilter>();

        /// <summary>
        /// The filters to run on audio coming in from the network before it is sent to output.
        /// Filters are run in the order they are placed to this list.
        /// </summary>
        public List<IAudioFilter> OutputFilters = new List<IAudioFilter>();

        /// <summary>
        /// A factory that returns an <see cref="IAudioOutput"/> 
        /// instance. Used every time a Peer connects to get an audio output for that peer.
        /// </summary>
        public IAudioOutputFactory OutputFactory { get; private set; }

        /// <summary>
        /// Map of the <see cref="IAudioOutput"/> for each peer in the audio chat.
        /// </summary>
        public Dictionary<int, IAudioOutput> PerPeerOutputs;

        /// <summary>
        /// Fired when the <see cref="CurrentMode"/> changes.
        /// </summary>
        public Action<AudioChatMode> OnModeChanged;

        /// <summary>
        /// The current <see cref="AudioChatMode"/> of this agent
        /// </summary>
        public AudioChatMode CurrentMode {
            get => _currentMode;
            private set {
                if(_currentMode != value) {
                    _currentMode = value;
                    OnModeChanged?.Invoke(value);
                    Debug.unityLogger.Log(TAG, "CurrentMode changed to " + value);
                }
            }
        }
        AudioChatMode _currentMode = AudioChatMode.Idle;

        /// <summary>
        /// Mutes all the peers. If set to true, no incoming audio from other 
        /// peers will be played. If you want to selectively mute a peer, use
        /// the <see cref="PeerSettings.muteThem"/> flag in the 
        /// <see cref="PerPeerSettings"/> instance for that peer.
        /// Note that setting this will not change <see cref="PerPeerSettings"/>
        /// </summary>
        public bool MuteEveryone { get; set; }

        /// <summary>
        /// Whether this agent is muted or not. If set to true, voice data will
        /// not be sent to ANY peer. If you want to selectively mute yourself 
        /// to a peer, use the <see cref="PeerSettings.muteSelf"/> 
        /// flag in the <see cref="PerPeerSettings"/> instance for that peer.
        /// Note that setting this will not change <see cref="PerPeerSettings"/>
        /// </summary>
        public bool MuteSelf { get; set; }

        /// <summary>
        /// <see cref="PeerSettings"/> for each peer which allows you
        /// to access or change the settings for a specific peer. Use [id] to get
        /// settings for a peer
        /// </summary>
        public Dictionary<int, PeerSettings> PerPeerSettings;

        #endregion

        // ====================================================================
        #region CONSTRUCTION AND DISPOSAL
        // ====================================================================
        /// <summary>
        /// Creates and returns a new instance using the provided dependencies.
        /// This instance then makes the dependencies work together.
        /// </summary>
        /// 
        /// <param name="chatroomNetwork">The chatroom network implementation  
        /// for chatroom access and sending data to peers in a chatroom.
        /// </param>
        /// 
        /// <param name="audioInput">The source of the outgoing audio</param>
        /// 
        /// <param name="audioOutputFactory">
        /// The factory used for creating <see cref="IAudioOutput"/> instances 
        /// for peers so that incoming audio from peers can be played.
        /// </param>
        public AudioChat(
            IAudioNetwork chatroomNetwork,
            IAudioInput audioInput,
            IAudioOutputFactory audioOutputFactory
        ) {
            Input = audioInput ??
            throw new ArgumentNullException(nameof(audioInput));

            Network = chatroomNetwork ??
            throw new ArgumentNullException(nameof(chatroomNetwork));

            OutputFactory = audioOutputFactory ??
            throw new ArgumentNullException(nameof(audioOutputFactory));

            CurrentMode = AudioChatMode.Idle;
            MuteEveryone = false;
            MuteSelf = false;
            PerPeerSettings = new Dictionary<int, PeerSettings>();
            PerPeerOutputs = new Dictionary<int, IAudioOutput>();

            Debug.unityLogger.Log(TAG, "Created");
            SetupEventListeners();
        }

        /// <summary>
        /// Disposes the instance. WARNING: Calling this method will
        /// also dispose the dependencies passed to it in the constructor.
        /// Be mindful of this if you're sharing dependencies between multiple
        /// instances and/or using them outside this instance.
        /// </summary>
        public void Dispose() {
            Debug.unityLogger.Log(TAG, "Disposing");
            Input.Dispose();

            RemoveAllPeers();
            PerPeerSettings.Clear();
            PerPeerOutputs.Clear();

            Network.Dispose();
            Debug.unityLogger.Log(TAG, "Disposed");
        }
        #endregion

        // ====================================================================
        #region INTERNAL 
        // ====================================================================
        void SetupEventListeners() {
            Debug.unityLogger.Log(TAG, "Setting up events.");

            // Network events
            Network.OnCreatedChatroom += () => {
                Debug.unityLogger.Log(TAG, "Chatroom created.");
                CurrentMode = AudioChatMode.Host;
            };
            Network.OnClosedChatroom += () => {
                Debug.unityLogger.Log(TAG, "Chatroom closed.");
                RemoveAllPeers();
                CurrentMode = AudioChatMode.Idle;
            };
            Network.OnJoinedChatroom += id => {
                Debug.unityLogger.Log(TAG, "Joined chatroom.");
                CurrentMode = AudioChatMode.Guest;
            };
            Network.OnLeftChatroom += () => {
                Debug.unityLogger.Log(TAG, "Left chatroom.");
                RemoveAllPeers();
                CurrentMode = AudioChatMode.Idle;
            };
            Network.OnPeerJoinedChatroom += id => {
                Debug.unityLogger.Log(TAG, "New peer joined: " + id);
                AddPeer(id);
            };
            Network.OnPeerLeftChatroom += id => {
                Debug.unityLogger.Log(TAG, "Peer left: " + id);
                RemovePeer(id);
            };

            // Stream the incoming audio data using the right peer output
            Network.OnAudioReceived += (peerID, data) => {
                // if we're muting all, do nothing.
                if (MuteEveryone) return;

                // Apply any filters
                if (OutputFilters != null)
                    foreach (var filter in OutputFilters)
                        if (filter != null)
                            data.samples = filter.Run(data.samples);

                if (AllowsIncomingAudioFromPeer(peerID)) {
                    PerPeerOutputs[peerID].Feed(data);
                }
            };

            Input.OnSamplesReady += (index, samples) => {
                if (CurrentMode == AudioChatMode.Idle) return;
                
                // If we're muting ourselves to all, do nothing.
                if (MuteSelf) return;

                // Get all the recipients we haven't muted ourselves to
                var recipients = Network.PeerIDs
                    .Where(id => AllowsOutgoingAudioToPeer(id));

                int before = samples.Length;
                // Apply any filters
                if (InputFilters != null)
                    foreach (var filter in InputFilters)
                        if (filter != null)
                            samples = filter.Run(samples);

                if (samples.Length == 0) return;

                var segment = new AudioFrame {
                    timestamp = index,
                    frequency = Input.Frequency,
                    channelCount = Input.ChannelCount,
                    samples = samples
                };

                // Send the audio segment to every deserving recipient
                foreach (var recipient in recipients) {
                    Network.SendAudioFrame(recipient, segment);
                }
            };

            Debug.unityLogger.Log(TAG, "Event setup completed.");
        }

        void AddPeer(int id) {
            // Ensure no old settings or outputs exist for this ID.
            RemovePeer(id);

            
            var output = OutputFactory.Create(
                Input.Frequency,
                Input.ChannelCount,
                Input.Frequency * Input.ChannelCount / Input.SegmentRate
            );
            PerPeerSettings.Add(id, new PeerSettings());
            PerPeerOutputs.Add(id, output);
            Debug.unityLogger.Log(TAG, "Added peer " + id);
        }

        void RemovePeer(int id) {
            if (PerPeerSettings.ContainsKey(id)) {
                PerPeerSettings.Remove(id);
                Debug.unityLogger.Log(TAG, "Removed peer settings for ID " + id);
            }
            if (PerPeerOutputs.ContainsKey(id)) {
                PerPeerOutputs[id].Dispose();
                PerPeerOutputs.Remove(id);
                Debug.unityLogger.Log(TAG, "Removed peer output for ID " + id);
            }
        }

        bool AllowsIncomingAudioFromPeer(int id) {
            return PerPeerSettings.ContainsKey(id) && !PerPeerSettings[id].muteThem;
        }

        bool AllowsOutgoingAudioToPeer(int id) {
            return PerPeerSettings.ContainsKey(id) && !PerPeerSettings[id].muteSelf;
        }

        void RemoveAllPeers() {
            Debug.unityLogger.Log(TAG, "Removing all peers");
            foreach(var peer in Network.PeerIDs) 
                RemovePeer(peer);
        }
        #endregion
    }
}
