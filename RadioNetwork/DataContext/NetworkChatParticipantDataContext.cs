using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadioNetwork.DataContext
{
    public abstract class NetworkChatParticipantDataContext
    {
        public abstract bool IsClient
        {
            get;
        }
    }
}
