using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    /// <summary>
    /// Log of client actions.
    /// </summary>
    class ClientActivity
    {
        public NetworkChatParticipant Talker { get; set; }
        public DateTime Timestamp { get; set; }

        public ClientActivity(NetworkChatParticipant talker, DateTime timestamp)
        {
            Talker = talker;
            Timestamp = timestamp;
        }
    }
}
