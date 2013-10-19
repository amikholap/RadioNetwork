using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


[assembly: log4net.Config.XmlConfigurator(Watch = true)]


namespace Network
{
    public class Server
    {
        private List<Client> _clients;

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Namespace);

        public Server()
        {
            _clients = new List<Client>();
        }

        /// <summary>
        /// Listens on BROADCAST_PORT for any udp datagrams with client ip addresses.
        /// Replies with an udp datagram in format "server\n<server_ip_addr>"
        /// </summary>
        public void ListenNewClients()
        {
            IPAddress clientAddr;
            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.BROADCAST_PORT);

            // listen for new clients
            while (true)
            {
                // init udp client
                UdpClient client = new UdpClient(Network.Properties.Settings.Default.BROADCAST_PORT);
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.EnableBroadcast = true;

                // recieve client
                byte[] dgram = client.Receive(ref broadcastEP);
                string data = Encoding.ASCII.GetString(dgram);
                try
                {
                    // data is in format {client,server}\n<ip_address>
                    string type = data.Split('\n')[0];
                    string sAddr = data.Split('\n')[1];

                    if (type.Equals("client"))
                    {
                        // get new client's IP address
                        clientAddr = IPAddress.Parse(sAddr);
                    }
                    else
                    {
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    log.Warn(ex);
                    client.Close();
                    continue;
                }
                log.Debug(String.Format("Found new client: {0}", clientAddr));

                // send a response to the connected client: "server\n<server_ip>"
                string response = String.Format("server\n{0}", NetworkHelper.GetLocalIPAddress());
                dgram = Encoding.ASCII.GetBytes(response);
                try
                {
                    client.Connect(clientAddr, Network.Properties.Settings.Default.BROADCAST_PORT);
                    client.Send(dgram, dgram.Length);
                }
                catch (Exception ex)
                {
                    log.Warn(ex);
                    continue;
                }
                finally
                {
                    client.Close();
                }
            }
        }

        public void ListenTCP()
        {
            TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 59555);
            listener.Start();

            while (true)
            {
                using (TcpClient c = listener.AcceptTcpClient())
                using (NetworkStream ns = c.GetStream())
                {
                    var buf = new byte[100];
                    ns.Read(buf, 0, buf.Length);
                    Server.log.Debug(Encoding.UTF8.GetString(buf));
                }
                Thread.Sleep(100);
            }
        }

        public void WriteTCP()
        {
            using (TcpClient client = new TcpClient("127.0.0.1", 59555))
            using (NetworkStream ns = client.GetStream())
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes("message");
                ns.Write(data, 0, data.Length);
            }
        }

        public void Run()
        {
            Thread listenNewClientsThread = new Thread(this.ListenNewClients);
            Thread listenTcpThread = new Thread(this.ListenTCP);

            listenNewClientsThread.Start();
            listenTcpThread.Start();
        }
    }
}
