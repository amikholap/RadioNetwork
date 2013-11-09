using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadioNetwork
{
    class ClientDataContext
    {
        private string _callsign;
        private int _fr, _ft;

        public ClientDataContext()
        {
            _callsign = "Тополь";
            _fr = 255;
            _ft = 375;
        }

        public string Callsign
        {
            get { return _callsign; }
            set
            {
                _callsign = value;
                if (String.IsNullOrWhiteSpace(value))
                {
                    throw new ApplicationException("Позывной не указан.");
                }
            }
        }

        public string Fr
        {
            get
            {
                return _fr.ToString();
            }
            set
            {
                if (!int.TryParse(value, out _fr) || _fr <= 0)
                {
                    throw new ApplicationException("Неправильное значение частоты приема.");
                }
            }
        }

        public string Ft
        {
            get { return _ft.ToString(); }
            set
            {
                if (!int.TryParse(value, out _ft) || _ft <= 0)
                {
                    throw new ApplicationException("Неправильное значение частоты приема.");
                }
            }
        }
    }
}
