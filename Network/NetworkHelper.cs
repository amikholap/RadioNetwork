using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    public class NetworkHelper
    {
        public static IPAddress GetLocalIPAddress()
        {
            IPAddress result = null;
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress addr in host.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    result = addr;
                    break;
                }
            }

            return result;
        }
    }
}