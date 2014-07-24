using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    public class MessagesChangedEventArgs : EventArgs
    {
        public readonly NetworkChatParticipant Talker;

        public readonly string Message;

        public MessagesChangedEventArgs(NetworkChatParticipant talker, string message)
        {
            Talker = talker;
            Message = message;
        }
    }
}
