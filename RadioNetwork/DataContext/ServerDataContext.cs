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
            _object = server;
        }

        public ObservableCollection<Client> Clients
        {
            get
            {
                return ((Server)_object).Clients;
            }
        }
    }
}
