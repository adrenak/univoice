using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Handles a client session. 
    /// Requires implementations of <see cref="IAudioClient{T}"/>, <see cref="IAudioInput"/> and <see cref="IAudioOutput"/>.
    /// Handles input, output along with filters over the entire client lifecycle.
    /// Adjusts to changes in configuration at runtime.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ClientSession<T> : IDisposable {
        /// <summary>
        /// Represents a filter registered in the session.
        /// Currently used only for output filters.
        /// </summary>
        class FilterFactoryEntry {
            public Type FilterType { get; }
            public Func<IAudioFilter> Factory { get; }

            public FilterFactoryEntry(Type filterType, Func<IAudioFilter> factory) {
                FilterType = filterType;
                Factory = factory;
            }
        }

        #region AUDIO OUTPUT 

        List<FilterFactoryEntry> outputFilterFactories = new List<FilterFactoryEntry>();
        Dictionary<T, List<IAudioFilter>> peerOutputFilters = new Dictionary<T, List<IAudioFilter>>();

        /// <summary>
        /// Whether any incoming audio from peers would be processed. If set to false, all incoming peer audio is ignored, and would
        /// neither be processed by the <see cref="OutputFilters"/> nor output to the <see cref="IAudioOutput"/> of any peer.
        /// This can be used to easily mute all the peers on the network.
        /// Note that this doesn't stop the audio data from arriving and would consume bandwidth. To stop reception completely
        /// by telling the server to not send audio, use <see cref="IAudioClient{T}.YourVoiceSettings"/>
        /// </summary>
        public bool OutputsEnabled { get; set; } = true;

        /// <summary>
        /// The <see cref="IAudioOutput"/> instances of each peer in the session
        /// </summary>
        public Dictionary<T, IAudioOutput> PeerOutputs { get; private set; } = new Dictionary<T, IAudioOutput>();

        /// <summary>
        /// Adds an filter to the output audio. Note: it is possible to register the same
        /// filter type more than once, this can be used to create some effects but can also cause
        /// errors.
        /// </summary>
        /// <typeparam name="TFilter">The type of the filter to be added</typeparam>
        /// <param name="filterFactory">A lambda method that returns an instance of the filter type</param>
        public void AddOutputFilter<TFilter>(Func<IAudioFilter> filterFactory)
        where TFilter : IAudioFilter {
            outputFilterFactories.Add(new FilterFactoryEntry(typeof(TFilter), filterFactory));

            foreach (var peerFilters in peerOutputFilters.Values)
                peerFilters.Add(filterFactory());
        }

        /// <summary>
        /// Checks if an output audio filter of a specific type has been registered.
        /// </summary>
        /// <typeparam name="TFilter">The type of the filter to check</typeparam>
        /// <returns>True if the filter is registered, false otherwise</returns>
        public bool HasOutputFilter<TFilter>()
            where TFilter : IAudioFilter {
            return outputFilterFactories.Any(entry => entry.FilterType == typeof(TFilter));
        }

        /// <summary>
        /// Removes a previously registered output audio filter
        /// </summary>
        /// <typeparam name="TFilter">The type of the filter to be removed</typeparam>
        public void RemoveOutputFilter<TFilter>()
        where TFilter : IAudioFilter {
            outputFilterFactories.RemoveAll(entry => entry.FilterType == typeof(TFilter));

            foreach (var peerFilters in peerOutputFilters.Values)
                peerFilters.RemoveAll(f => f.GetType() == typeof(TFilter));
        }

        #endregion

        #region AUDIO INPUT

        /// <summary>
        /// Whether input audio will be processed. If set to false, any input audio captured by 
        /// <see cref="Input"/> would be ignored and would neither be processed by the <see cref="InputFilters"/> nor send via the <see cref="Client"/>
        /// This can be used to create "Push to talk" style features without having to use <see cref="IAudioClient{T}.YourVoiceSettings"/>
        /// </summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>
        /// The <see cref="IAudioFilter"/> that will be applied to the outgoing audio for all the peers.
        /// Note that filters are executed in the order they are present in this list
        /// </summary>
        public List<IAudioFilter> InputFilters { get; set; } = new List<IAudioFilter>();

        /// <summary>
        /// Checks if an input audio filter of a specific type has been registered.
        /// </summary>
        /// <typeparam name="TFilter">The type of the filter to check</typeparam>
        /// <returns>True if the filter is registered, false otherwise</returns>
        public bool HasInputFilter<TFilter>()
            where TFilter : IAudioFilter {
            return InputFilters.Any(filter => filter.GetType() == typeof(TFilter));
        }

        #endregion

        public ClientSession(IAudioClient<T> client, IAudioInput input, Func<IAudioOutput> outputProvider) {
            Client = client;
            Input = input;
            OutputProvider = outputProvider;
        }

        public ClientSession(IAudioClient<T> client, IAudioInput input, IAudioOutputFactory outputFactory) {
            Client = client;
            Input = input;
            OutputFactory = outputFactory;
        }

        /// <summary>
        /// The <see cref="IAudioClient{T}"/> that's used for networking
        /// </summary>
        IAudioClient<T> client;
        public IAudioClient<T> Client {
            get => client;
            set {
                if (client != null)
                    client.Dispose();
                client = value;

                Client.OnLeft += () => {
                    foreach (var output in PeerOutputs)
                        output.Value.Dispose();
                    PeerOutputs.Clear();
                    peerOutputFilters.Clear();
                };

                Client.OnPeerJoined += id => {
                    try {
                        if (OutputProvider != null) {
                            var output = OutputProvider();
                            PeerOutputs.Add(id, output);
                        }
                        else if (OutputFactory != null) {
                            var output = OutputFactory.Create();
                            PeerOutputs.Add(id, output);
                        }

                        var filters = outputFilterFactories
                            .Select(entry => entry.Factory())
                            .ToList();
                        peerOutputFilters.Add(id, filters);
                    }
                    catch (Exception e) {
                        Debug.LogException(e);
                    }
                };

                Client.OnPeerLeft += id => {
                    if (PeerOutputs.ContainsKey(id)) {
                        PeerOutputs[id].Dispose();
                        PeerOutputs.Remove(id);
                    }

                    if (peerOutputFilters.ContainsKey(id)) {
                        peerOutputFilters.Remove(id);
                    }
                };

                Client.OnReceivedPeerAudioFrame += (id, audioFrame) => {
                    if (!OutputsEnabled || !PeerOutputs.ContainsKey(id))
                        return;

                    if (peerOutputFilters.TryGetValue(id, out var filters)) {
                        foreach (var filter in filters)
                            audioFrame = filter.Run(audioFrame);
                    }

                    if (audioFrame.samples.Length > 0)
                        PeerOutputs[id]?.Feed(audioFrame);
                };
            }
        }

        IAudioInput input;
        /// <summary>
        /// The <see cref="IAudioInput"/> that's used for sourcing outgoing audio
        /// </summary>
        public IAudioInput Input {
            get => input;
            set {
                if (input != null)
                    input.Dispose();
                input = value;
                input.OnFrameReady += frame => {
                    if (!InputEnabled)
                        return;

                    if (InputFilters != null) {
                        foreach (var filter in InputFilters)
                            frame = filter.Run(frame);
                    }

                    if (frame.samples.Length > 0)
                        Client.SendAudioFrame(frame);
                };
            }
        }

        Func<IAudioOutput> outputProvider;
        /// <summary>
        /// The provider of IAudioOutput objects for peers.
        /// If this value is being set while peers already exist,
        /// the old outputs would be cleared and new onces will 
        /// be created.
        /// </summary>
        public Func<IAudioOutput> OutputProvider {
            get => outputProvider;
            set {
                outputProvider = value;
                outputFactory = null;

                foreach (var output in PeerOutputs)
                    output.Value.Dispose();
                PeerOutputs.Clear();

                if(outputProvider != null) {
                    foreach (var id in Client.PeerIDs) {
                        try {
                            var output = outputProvider();
                            PeerOutputs.Add(id, output);
                        }
                        catch (Exception e) {
                            Debug.LogException(e);
                        }
                    }
                }
            }
        }


        IAudioOutputFactory outputFactory;
        /// <summary>
        /// The <see cref="IAudioOutputFactory"/> that creates the <see cref="IAudioOutput"/> of peers
        /// </summary>
        public IAudioOutputFactory OutputFactory {
            get => outputFactory;
            set {
                outputFactory = value;
                outputProvider = null;

                foreach (var output in PeerOutputs)
                    output.Value.Dispose();
                PeerOutputs.Clear();

                if (outputFactory != null) {
                    foreach (var id in Client.PeerIDs) {
                        try {
                            var output = outputFactory.Create();
                            PeerOutputs.Add(id, output);
                        }
                        catch (Exception e) {
                            Debug.LogException(e);
                        }
                    }
                }
            }
        }

        public void Dispose() {
            Client.Dispose();
            Input.Dispose();
        }

        #region OBSOLETE

        /// <summary>
        /// The output <see cref="IAudioFilter"/> that will be applied to the incoming audio for all the peers.
        /// Note that filters are executed in the order they are present in this list.
        /// </summary>
        [Obsolete("OutputFilters has been removed. Use AddOutputFilter and RemoveOutputFilter instead.", true)]
        public List<IAudioFilter> OutputFilters { get; set; } = new List<IAudioFilter>();

        #endregion
    }
}
