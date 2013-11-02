﻿using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Network
{
    public class Server
    {
        /// <summary>
        /// Indicates if server is running.
        /// True by default.
        /// When set to false server will shut down in several seconds.
        /// </summary>
        private bool _isWorking;
        private List<Client> _clients;

        private static readonly ILog log = LogManager.GetLogger("RadioNetwork");

        public Server()
        {
            _isWorking = true;
            _clients = new List<Client>();
        }

        /// <summary>
        /// Listens on BROADCAST_PORT for any udp datagrams with client ip addresses.
        /// Replies with a string "server"
        /// </summary>
        public void ListenNewClients()
        {
            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.BROADCAST_PORT);
            UdpClient client = new UdpClient(Network.Properties.Settings.Default.BROADCAST_PORT);
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.EnableBroadcast = true;

            // listen for new clients
            while (_isWorking)
            {
                while (client.Available > 0)
                {
                    // receive client's message
                    byte[] dgram = client.Receive(ref broadcastEP);
                    IPAddress clientAddr = broadcastEP.Address;
                    string type = Encoding.UTF8.GetString(dgram);    // either "client" or "server"
                    if (type == "server")
                    {
                        // skip server's own messages
                        continue;
                    }

                    log.Debug(String.Format("Found client: {0}", clientAddr));

                    // send a string "server" to the connected client
                    string response = "server";
                    dgram = Encoding.UTF8.GetBytes(response);
                    UdpClient c = new UdpClient();
                    try
                    {
                        c.Connect(clientAddr, Network.Properties.Settings.Default.BROADCAST_PORT);
                        c.Send(dgram, dgram.Length);
                    }
                    catch (Exception e)
                    {
                        log.Error(e);
                        continue;
                    }
                    finally
                    {
                        c.Close();
                    }
                }
                Thread.Sleep(50);
            }
        }

        /// <summary>
        /// Listens on TCP_PORT for any client actions such as
        /// conecting and updating 
        /// </summary>
        public void ListenClientsInfo()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, Network.Properties.Settings.Default.TCP_PORT);
            listener.Server.ReceiveTimeout = 5000;
            listener.Server.SendTimeout = 5000;
            listener.Start();

            while (_isWorking)
            {
                if (listener.Pending())
                {
                    using (TcpClient tcpClient = listener.AcceptTcpClient())
                    using (NetworkStream ns = tcpClient.GetStream())
                    {
                        IPAddress clientAddr;    // client's IP address
                        string callsign;         // client's callsign
                        int fr, ft;              // client's receive and transmit frequencies

                        var buf = new byte[100];
                        ns.Read(buf, 0, buf.Length);
                        string request = Encoding.UTF8.GetString(buf);
                        log.Debug(String.Format("raw request:\n{0}", request));
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
                }
                else
                {
                    Thread.Sleep(500);
                }
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

        /// <summary>
        /// Launch the server.
        /// It spawns several threads for listening and processing
        /// client messages.
        /// </summary>
        public void Start()
        {
            Thread listenNewClientsThread = new Thread(this.ListenNewClients);
            Thread listenTcpThread = new Thread(this.ListenClientsInfo);

            listenNewClientsThread.Start();
            listenTcpThread.Start();
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public void Stop()
        {
            _isWorking = false;
        }
    }
}