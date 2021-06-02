using Adrenak.UniMic;

namespace Adrenak.UniVoice.InbuiltImplementations {
    public class InbuiltChatroomAgentFactory : IChatroomAgentFactory {
        public string SignallingServerURL { get; set; }

        public InbuiltChatroomAgentFactory(string signallingServerURL) =>
            this.SignallingServerURL = signallingServerURL;

        public ChatroomAgent Create() {
            var network = new AirPeerUniVoiceNetwork(SignallingServerURL);

            var input = new UniMicAudioInput(Mic.Instance);
            if (!Mic.Instance.IsRecording)
                Mic.Instance.StartRecording(16000, 100);

            var outputFactory = new InbuiltAudioOutputFactory();

            return new ChatroomAgent(network, input, outputFactory) {
                MuteSelf = false
            };
        }
    }
}
