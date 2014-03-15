using Network;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadioNetwork
{
    class ServerDataContext
    {
        private string _callsign;
        private Server _server;

        public ServerDataContext(Server server)
        {
            _callsign = "Береза";
            _server = server;
        }

        public string Callsign
        {
            get
            {
                return _callsign;
            }
            set
            {
                _callsign = value;
                if (String.IsNullOrWhiteSpace(value))
                {
                    throw new ApplicationException("Позывной не указан.");
                }
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
