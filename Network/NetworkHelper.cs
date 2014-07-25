using System;
using System.Collections;
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

            // Pairs of subnetAddr, subnetMask of local network address ranges from RFC 1918
            Tuple<IPAddress, IPAddress>[] localNetworkAddresses =
            {
                new Tuple<IPAddress, IPAddress>(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("255.0.0.0")),
                new Tuple<IPAddress, IPAddress>(IPAddress.Parse("172.16.0.0"), IPAddress.Parse("255.240.0.0")),
                new Tuple<IPAddress, IPAddress>(IPAddress.Parse("192.168.0.0"), IPAddress.Parse("255.255.0.0")),
            };

            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress addr in host.AddressList)
            {
                // Use only IPv4
                if (addr.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                // Use the address if it is in one of predefined local network address ranges
                foreach (var pair in localNetworkAddresses)
                {
                    BitArray h = new BitArray(addr.GetAddressBytes());
                    BitArray n = new BitArray(pair.Item1.GetAddressBytes());
                    BitArray m = new BitArray(pair.Item2.GetAddressBytes());

                    var x = h.And(m);
                    var z = BitArray.Equals(x, n);

                    bool equals = true;
                    for (int i = 0; i < n.Length; ++i)
                    {
                        if (n[i] != x[i])
                        {
                            equals = false;
                            break;
                        }
                    }
                    if (equals)
                    {
                        result = addr;
                        break;
                    }
                }

                // Find any IPv4 address
                if (result == null)
                {
                    result = addr;
                }
            }

            return result;
        }

        /// <summary>
        /// Derive a multicast group from the frequency.
        /// 239.XX.XX.XX
        /// f = 123 => 239.0.0.123
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static IPAddress FreqToMcastGroup(UInt32 f)
        {
            UInt32 intAddr = 0xef000000 | f;
            byte[] byteAddr = BitConverter.GetBytes(intAddr);
            if (BitConverter.IsLittleEndian)
                byteAddr = byteAddr.Reverse().ToArray();
            return new IPAddress(byteAddr);
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

        public static UdpClient InitUdpClient(IPEndPoint localEP)
        {
            UdpClient client = InitUdpClient();
            client.Client.Bind(localEP);
            return client;
        }

        public static UdpClient InitUdpClient(int port)
        {
            return InitUdpClient(new IPEndPoint(IPAddress.Any, port));
        }
    }
}