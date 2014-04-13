using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Network
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
