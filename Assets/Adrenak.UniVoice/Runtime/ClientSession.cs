using System;
using System.Collections.Generic;

using UnityEngine;

namespace Adrenak.UniVoice {
    public class ClientSession<T> : IDisposable {
        public Dictionary<T, IAudioOutput> PeerOutputs { get; private set; } = new Dictionary<T, IAudioOutput>();

        public List<IAudioFilter> InputFilters { get; set; } = new List<IAudioFilter>();
        public List<IAudioFilter> OutputFilters { get; set; } = new List<IAudioFilter>();

        public ClientSession(IAudioClient<T> client, IAudioInput input, IAudioOutputFactory outputFactory) {
            Client = client;
            Input = input;
            OutputFactory = outputFactory;
        }

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

                    PeerOutputs[id].Feed(audioFrame);
                };
            }
        }

        IAudioInput input;
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

                    Client.SendAudioFrame(frame);
                };
            }
        }

        IAudioOutputFactory outputFactory;
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
