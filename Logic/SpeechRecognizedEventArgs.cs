using Network;
using System;

namespace Logic
{
    public class SpeechRecognizedEventArgs : EventArgs
    {
        public readonly NetworkChatParticipant Talker;
        public readonly string Message;

        public SpeechRecognizedEventArgs(NetworkChatParticipant talker, string message)
        {
            Talker = talker;
            Message = message;
        }
    }
}
