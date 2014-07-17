using Network;
using RadioNetwork.DataContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadioNetwork
{
    class ClientDataContext : NetworkChatParticipantDataContext
    {
        private Client _client;

        public IEnumerable<ServerSummary> AvailableServers { get; set; }

        public ClientDataContext(Client client)
        {
            _client = client;
        }

        public override bool IsClient
        {
            get { return true; }
        }

        public string Callsign
        {
            get { return _client.Callsign; }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                {
                    throw new ApplicationException("Позывной не указан.");
                }
                _client.Callsign = value;
            }
        }

        public string Fr
        {
            get
            {
                return _client.Fr.ToString();
            }
            set
            {
                uint fr;
                if (!uint.TryParse(value, out fr))
                {
                    throw new ApplicationException("Неправильное значение частоты приема.");
                }
                _client.Fr = fr;
            }
        }

        public string Ft
        {
            get { return _client.Ft.ToString(); }
            set
            {
                uint ft;
                if (!uint.TryParse(value, out ft))
                {
                    throw new ApplicationException("Неправильное значение частоты приема.");
                }
                _client.Ft = ft;
            }
        }
    }
}
