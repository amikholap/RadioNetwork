using Network;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadioNetwork.DataContext
{
    public abstract class NetworkChatParticipantDataContext : INotifyPropertyChanged
    {
        protected NetworkChatParticipant _object;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public bool IsClient
        {
            get
            {
                return _object is Client;
            }
        }
        public bool IsServer
        {
            get
            {
                return _object is Server;
            }
        }

        public bool IsWorking
        {
            get
            {
                return _object.IsWorking;
            }
        }

        public string Callsign
        {
            get { return _object.Callsign; }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                {
                    throw new ApplicationException("Позывной не указан.");
                }
                _object.Callsign = value;
            }
        }
    }
}
