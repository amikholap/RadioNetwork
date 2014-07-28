using Network;
using RadioNetwork.DataContext;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadioNetwork
{
    class ServerDataContext : NetworkChatParticipantDataContext
    {
        public ServerDataContext(Server server)
        {
            Object = server;
        }

        public ObservableCollection<Client> Clients
        {
            get
            {
                return ((Server)Object).Clients;
            }
        }
    }
}
