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
        public IEnumerable<ServerSummary> AvailableServers { get; set; }

        public ClientDataContext(Client client)
        {
            Object = client;
        }

        public string Fr
        {
            get
            {
                if (((Client)Object).Fr == 0)
                {
                    return "";
                }
                return ((Client)Object).Fr.ToString();
            }
            set
            {
                UInt32 fr;
                if (String.IsNullOrEmpty(value))
                {
                    fr = 0;
                }
                else
                {
                    try
                    {
                        fr = UInt32.Parse(value);
                    }
                    catch (FormatException)
                    {
                        throw new ApplicationException("Неправильное значение частоты приема.");
                    }
                }
                ((Client)Object).Fr = fr;
                NotifyPropertyChanged("Fr");
            }
        }

        public string Ft
        {
            get
            {
                if (((Client)Object).Ft == 0)
                {
                    return "";
                }
                return ((Client)Object).Ft.ToString();
            }
            set
            {
                UInt32 ft;
                if (String.IsNullOrEmpty(value))
                {
                    ft = 0;
                }
                else
                {
                    try
                    {
                        ft = UInt32.Parse(value);
                    }
                    catch (FormatException)
                    {
                        throw new ApplicationException("Неправильное значение частоты передачи.");
                    }
                }
                ((Client)Object).Ft = ft;
                NotifyPropertyChanged("Ft");
            }
        }
    }
}
