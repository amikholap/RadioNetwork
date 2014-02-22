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

        /// <summary>
        /// Set send/receive timeouts and REUSE_ADDRESS socket option.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="timeout"></param>
        public static void ConfigureUdpClient(ref UdpClient client, int timeout = 1000)
        {
            client.Client.SendTimeout = timeout;
            client.Client.ReceiveTimeout = timeout;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public static UdpClient InitUdpClient()
        {
            UdpClient client = new UdpClient();
            ConfigureUdpClient(ref client);
            return client;
        }

        public static UdpClient InitUdpClient(int port)
        {
            UdpClient client = new UdpClient(port);
            ConfigureUdpClient(ref client);
            return client;
        }

        public static UdpClient InitUdpClient(IPEndPoint localEP)
        {
            UdpClient client = new UdpClient(localEP);
            ConfigureUdpClient(ref client);
            return client;
        }
    }
}