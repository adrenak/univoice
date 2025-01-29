﻿using System;
using System.Collections.Generic;

using UnityEngine;

namespace Adrenak.UniVoice {
    /// <summary>
    /// Handles a client session. 
    /// Requires an implementation of <see cref="IAudioClient{T}"/>, <see cref="IAudioInput"/> and <see cref="IAudioOutputFactory"/> each.
    /// Allows adding input and output filters and handles their execution.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ClientSession<T> : IDisposable {
        /// <summary>
        /// The <see cref="IAudioOutput"/> instances of each peer in the session
        /// </summary>
        public Dictionary<T, IAudioOutput> PeerOutputs { get; private set; } = new Dictionary<T, IAudioOutput>();

        /// <summary>
        /// The input <see cref="IAudioFilter"/> that will be applied to the outgoing audio for all the peers.
        /// Note that filters are executed in the order they are present in this list
        /// </summary>
        public List<IAudioFilter> InputFilters { get; set; } = new List<IAudioFilter>();

        /// <summary>
        /// The output <see cref="IAudioFilter"/> that will be applied to the incoming audio for all the peers.
        /// Note that filters are executed in the order they are present in this list.
        /// </summary>
        public List<IAudioFilter> OutputFilters { get; set; } = new List<IAudioFilter>();

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
                if(client != null)
                    client.Dispose();
                client = value;

                Client.OnLeft += () => {
                    foreach (var output in PeerOutputs)
                        output.Value.Dispose();
                    PeerOutputs.Clear();
                };

                Client.OnPeerJoined += id => {
                    try {
                        var output = outputFactory.Create();
                        PeerOutputs.Add(id, output);
                    }
                    catch (Exception e) {
                        Debug.LogException(e);
                    }
                };

                Client.OnPeerLeft += id => {
                    if (!PeerOutputs.ContainsKey(id)) 
                        return;

                    PeerOutputs[id].Dispose();
                    PeerOutputs.Remove(id);
                };

                client.OnReceivedPeerAudioFrame += (id, audioFrame) => {
                    if (!PeerOutputs.ContainsKey(id))
                        return;

                    if (OutputFilters != null) {
                        foreach (var filter in OutputFilters)
                            audioFrame = filter.Run(audioFrame);
                    }
                    if(audioFrame.samples.Length > 0)
                        PeerOutputs[id].Feed(audioFrame);
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
                if(input != null)
                    input.Dispose();
                input = value;
                input.OnFrameReady += frame => {
                    if (InputFilters != null) {
                        foreach (var filter in InputFilters)
                            frame = filter.Run(frame);
                    }

                    if(frame.samples.Length > 0)
                        Client.SendAudioFrame(frame);
                };
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

                foreach (var output in PeerOutputs)
                    output.Value.Dispose();
                PeerOutputs.Clear();

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

        public void Dispose() {
            Client.Dispose();
            Input.Dispose();
        }
    }
}
