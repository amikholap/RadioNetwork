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
        private Server _server;

        public ServerDataContext(Server server)
        {
            _server = server;
        }

        public override bool IsClient
        {
            get { return false; }
        }

        public string Callsign
        {
            get
            {
                return _server.Callsign;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                {
                    throw new ApplicationException("Позывной не указан.");
                }
                _server.Callsign = value;
            }
        }

        public ObservableCollection<Client> Clients
        {
            get
            {
                return _server.Clients;
            }
        }
    }
}
