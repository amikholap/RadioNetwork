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

        public abstract bool IsClient
        {
            get;
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
