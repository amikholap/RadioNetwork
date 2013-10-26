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

        private static readonly ILog log = LogManager.GetLogger("RadioNetwork");

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

                // receive client's message
                byte[] dgram = client.Receive(ref broadcastEP);
                string data = Encoding.UTF8.GetString(dgram);
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
                catch (Exception e)
                {
                    log.Error(e);
                    client.Close();
                    continue;
                }
                log.Debug(String.Format("Found new client: {0}", clientAddr));

                // send a response to the connected client: "server\n<server_ip>"
                string response = String.Format("server\n{0}", NetworkHelper.GetLocalIPAddress());
                dgram = Encoding.UTF8.GetBytes(response);
                try
                {
                    client.Connect(clientAddr, Network.Properties.Settings.Default.BROADCAST_PORT);
                    client.Send(dgram, dgram.Length);
                }
                catch (Exception e)
                {
                    log.Error(e);
                    continue;
                }
                finally
                {
                    client.Close();
                }
            }
        }

        /// <summary>
        /// Listens on TCP_PORT for any client actions such as
        /// conecting and updating 
        /// </summary>
        public void ListenClientsInfo()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, Network.Properties.Settings.Default.TCP_PORT);
            listener.Start();

            while (true)
            {
                using (TcpClient tcpClient = listener.AcceptTcpClient())
                using (NetworkStream ns = tcpClient.GetStream())
                {
                    IPAddress clientAddr;    // client's IP address
                    string callsign;       // client's callsign
                    int fr, ft;            // client's receive and transmit frequencies

                    var buf = new byte[100];
                    ns.Read(buf, 0, buf.Length);
                    string request = Encoding.UTF8.GetString(buf);
                    log.Debug(String.Format("raw request: {0}", request));
                    string[] lines = request.Split('\n');

                    switch (lines[0])    // first line specifies the action
                    {
                        case "UPDATE":
                            // the message format is:
                            //     UPDATE
                            //     <callsign>
                            //     <receive_frequency>,<transmit_frequency>
                            try
                            {
                                callsign = lines[1];
                                fr = int.Parse(lines[2].Split(',')[0]);
                                ft = int.Parse(lines[2].Split(',')[1]);
                            }
                            catch (Exception e)
                            {
                                log.Error(e.Message);
                                continue;
                            }

                            // get client's IP address
                            clientAddr = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address;

                            // add a new client to the list only if
                            // a client with such IP address doesn't exist
                            if (!_clients.Exists(c => c.Addr == clientAddr))
                            {
                                _clients.Add(new Client(clientAddr, callsign, fr, ft));
                                log.Debug(String.Format("Client connected: {0} with freqs {1}, {2}", callsign, fr, ft));
                                break;
                            }

                            // a client with such IP address already exists
                            // update it
                            Client client = _clients.Find(c => c.Addr == clientAddr);
                            client.Callsign = callsign;
                            client.Fr = fr;
                            client.Ft = ft;

                            break;
                        default:
                            // unknown format
                            Server.log.Warn(request);
                            continue;
                    }
                }
                Thread.Sleep(100);
            }
        }

        public void WriteTCP()
        {
            using (TcpClient client = new TcpClient())
            {
                client.Connect(new IPEndPoint(IPAddress.Loopback, Network.Properties.Settings.Default.TCP_PORT));
                using (NetworkStream ns = client.GetStream())
                {
                    string request = "UPDATE\nТополь\n175,275";
                    byte[] data = Encoding.UTF8.GetBytes(request);
                    ns.Write(data, 0, data.Length);
                }
            }
        }

        public void Run()
        {
            Thread listenNewClientsThread = new Thread(this.ListenNewClients);
            Thread listenTcpThread = new Thread(this.ListenClientsInfo);

            listenNewClientsThread.Start();
            listenTcpThread.Start();
        }
    }
}
