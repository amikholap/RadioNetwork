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
        private NetworkChatParticipant _object;
        protected NetworkChatParticipant Object
        {
            get
            {
                return _object;
            }
            set
            {
                _object = value;
                _object.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == "IsWorking")
                    {
                        this.NotifyPropertyChanged("IsWorking");
                    }
                };
            }
        }

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
                return Object is Client;
            }
        }
        public bool IsServer
        {
            get
            {
                return Object is Server;
            }
        }

        public bool IsWorking
        {
            get
            {
                return Object.IsWorking;
            }
        }

        public string Callsign
        {
            get { return Object.Callsign; }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                {
                    throw new ApplicationException("Позывной не указан.");
                }
                Object.Callsign = value;
            }
        }
    }
}
