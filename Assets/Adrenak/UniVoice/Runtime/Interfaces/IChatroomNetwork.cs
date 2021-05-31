using System;
using System.Collections.Generic;

namespace Adrenak.UniVoice {
    public interface IChatroomNetwork : IDisposable {
        event Action OnChatroomCreated;
        event Action<Exception> OnChatroomCreationFailed;
        event Action OnChatroomClosed;

        event Action<short> OnJoined;
        event Action OnLeft;

        event Action<short> OnPeerJoined;
        event Action<short> OnPeerLeft;

        event Action<short, int, int, int, float[]> OnAudioReceived;
        event Action<short, int, int, int, float[]> OnAudioSent;

        short ID { get; }
        List<short> Peers { get; }
        string CurrentRoomName { get; }

        void CreateChatroom(string chatroomName);
        void CloseChatroom();
        void JoinChatroom(string chatroomName);
        void LeaveChatroom();

        void SendAudioSegment(short recipientID, int segmentIndex, int frequency, int channelCount, float[] samples);
    }
}
