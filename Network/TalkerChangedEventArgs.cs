using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    public class TalkerChangedEventArgs : EventArgs
    {
        public readonly NetworkChatParticipant PrevTalker;
        public readonly NetworkChatParticipant NewTalker;

        public TalkerChangedEventArgs(NetworkChatParticipant prevTalker, NetworkChatParticipant newTalker)
        {
            PrevTalker = prevTalker;
            NewTalker = newTalker;
        }
    }
}
