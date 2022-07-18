using Adrenak.UniMic;

namespace Adrenak.UniVoice.InbuiltImplementations {
    public class InbuiltChatroomAgentFactory : IChatroomAgentFactory {
        public string SignalingServerURL { get; set; }

        public InbuiltChatroomAgentFactory(string signalingServerURL) =>
            this.SignalingServerURL = signalingServerURL;

        public ChatroomAgent Create() {
            var network = new AirPeerUniVoiceNetwork(SignalingServerURL);

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
