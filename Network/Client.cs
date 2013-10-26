using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    public class Client
    {
        public IPAddress Addr { get; set; }
        public string Callsign { get; set; }
        public int Fr { get; set; }
        public int Ft { get; set; }

        public Client(IPAddress addr, string nickname, int fr, int ft)
        {
            Addr = addr;
            Callsign = nickname;
            Fr = fr;
            Ft = ft;
        }
    }
}
