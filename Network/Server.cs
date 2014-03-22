using Audio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Network
{
    public class Server : NetworkChatParticipant
    {
        /// <summary>
        /// Indicates if server is running.
        /// True by default.
        /// When set to false server will shut down in several seconds.
        /// </summary>
        private volatile bool _isWorking;

        private int pingSendWaitTimeOut = 1000;

        /// <summary>
        /// Cliens collection should be modified only from
        /// WPF dispacher thread to be properly updated on UI.
        /// To accomplish this use Dispatcher.Invoke.
        /// </summary>
        private ObservableCollection<Client> _clients;
        private Dictionary<IPAddress, UdpClient> _mcastClients;

        public ObservableCollection<Client> Clients
        {
            get
            {
                return _clients;
            }
        }

        public Server()
            : base()
        {
            _isWorking = true;
            _clients = new ObservableCollection<Client>();
            _mcastClients = new Dictionary<IPAddress, UdpClient>();
        }

        /// <summary>
        /// Listen on BROADCAST_PORT for any udp datagrams with client ip addresses.
        /// Reply with a string "server"
        /// </summary>
        private void ListenNewClients()
        {
            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.BROADCAST_PORT);
            UdpClient client = NetworkHelper.InitUdpClient(Network.Properties.Settings.Default.BROADCAST_PORT);
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

                    logger.Debug(String.Format("Found client: {0}", clientAddr));

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
                        logger.Error("Unhandled exception while listening for new clients.", e);
                        continue;
                    }
                    finally
                    {
                        c.Close();
                    }
                }
                Thread.Sleep(50);
            }
            client.Close();
        }

        /// <summary>
        /// Listens on TCP_PORT for any client actions such as
        /// conecting and updating 
        /// </summary>
        private void ListenClientsInfo()
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
                        UInt32 fr, ft;           // client's receive and transmit frequencies

                        var buf = new byte[Network.Properties.Settings.Default.BUFFER_SIZE];
                        ns.Read(buf, 0, buf.Length);
                        string request = Encoding.UTF8.GetString(buf);
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
                                    fr = UInt32.Parse(lines[2].Split(',')[0]);
                                    ft = UInt32.Parse(lines[2].Split(',')[1]);
                                }
                                catch (Exception e)
                                {
                                    logger.Error("Unhandled exception while listening clients' info.", e);
                                    continue;
                                }

                                // get client's IP address
                                clientAddr = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address;

                                // add a new client to the list only if
                                // a client with such IP address doesn't exist
                                Client existingClient = _clients.FirstOrDefault(c => c.Addr == clientAddr);
                                if (existingClient == null)
                                {
                                    Dispatcher.Invoke((Action)(() => _clients.Add(new Client(clientAddr, callsign, fr, ft))));
                                    logger.Debug(String.Format("Client connected: {0} with freqs {1}, {2}", callsign, fr, ft));
                                }
                                else
                                {
                                    // a client with such IP address already exists
                                    // update it
                                    existingClient.Callsign = callsign;
                                    existingClient.Fr = fr;
                                    existingClient.Ft = ft;
                                }
                                UpdateMulticastClients();

                                break;
                            default:
                                // unknown format
                                Server.logger.Warn(request);
                                continue;
                        }
                    }
                }
                else
                {
                    Thread.Sleep(500);
                }
            }
            listener.Stop();
        }

        public void StartPing()
        {
            int TimeOut = pingSendWaitTimeOut;
            base._connectPing = true;
            while (base._connectPing == true)
            {
                foreach (Client client in _clients.ToArray())
                {
                    if (base.StartAsyncPing(client.Addr, Network.Properties.Settings.Default.PING_PORT_OUT_SERVER) == false)
                    {

                        Dispatcher.Invoke((Action)(() => _clients.Remove(client)));  // remove non-ping client
                    }
                }
                Thread.Sleep(TimeOut);
            }
        }

        public void StartConnectPingThread()
        {
            base._connectPingThread = new Thread(() => this.StartPing());
            base._connectPingThread.Start();
        }

        public void StopConnectPingThread()
        {
            lock (this)
            {
                base._connectPing = false;
            }
        }

        /// <summary>
        /// Launch the server.
        /// It spawns several threads for listening and processing.
        /// client messages.
        /// </summary>
        /// <summary>
        /// Update UDPClients for multicast groups.
        /// </summary>
        private void UpdateMulticastClients()
        {
            // already initialized multicast groups
            var currentAddrs = _mcastClients.Keys;
            // a fresh list of required mmulticast groups
            var newAddrs = _clients.Select(c => c.TransmitMulticastGroupAddr).Distinct().ToArray();

            List<IPAddress> toAdd = newAddrs.Except(currentAddrs).ToList();
            List<IPAddress> toRemove = currentAddrs.Except(newAddrs).ToList();

            // initialize UdpClients for new clients
            foreach (IPAddress addr in toAdd)
            {
                UdpClient client = NetworkHelper.InitUdpClient();
                client.JoinMulticastGroup(addr);
                _mcastClients[addr] = client;
            }

            // close UdpClients for disconnected clients
            foreach (IPAddress addr in toRemove)
            {
                UdpClient client = _mcastClients[addr];
                _mcastClients.Remove(addr);
                client.DropMulticastGroup(addr);
                client.Close();
            }
        }

        protected override void StartStreamingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.BUFFER_SIZE];

            while (true)
            {
                _micPipe.Read(buffer, 0, buffer.Length);
                foreach (var item in _mcastClients.ToArray())
                {
                    item.Value.Send(buffer, buffer.Length, new IPEndPoint(item.Key, Network.Properties.Settings.Default.MULTICAST_PORT));
                }
            }
        }

        protected override void StartReceivingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.BUFFER_SIZE];
            ClientActivity lastActive = new ClientActivity();

            IPEndPoint clientEP = null;
            IPEndPoint anyClientEP = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.SERVER_AUDIO_PORT);
            UdpClient client = NetworkHelper.InitUdpClient(anyClientEP);

            _receiving = true;
            while (_receiving)
            {
                try
                {
                    buffer = client.Receive(ref clientEP);
                }
                catch (SocketException)
                {
                    // timeout
                    continue;
                }

                // change active client if it wasn't set or was silent for too long
                if (lastActive.Client == null || DateTime.Now - lastActive.last_talked > TimeSpan.FromSeconds(0.5))
                {
                    lastActive.Client = _clients.FirstOrDefault(c => c.Addr.Equals(clientEP.Address));
                }

                // process audio data only from active client
                if (lastActive.Client != null && clientEP.Address.Equals(lastActive.Client.Addr))
                {
                    // add received data to the player queue
                    AudioHelper.AddSamples(buffer);

                    // spread server message to all clients
                    foreach (var item in _mcastClients.ToArray())
                    {
                        item.Value.Send(buffer, buffer.Length, new IPEndPoint(item.Key, Network.Properties.Settings.Default.MULTICAST_PORT));
                    }

                    // update last_talked timestamp
                    lastActive.last_talked = DateTime.Now;
                }
            }

            client.Close();
        }

        /// <summary>
        /// Launch the server.
        /// It spawns several threads for listening and processing.
        /// client messages.
        /// </summary>
        public override void Start()
        {
            base.Start();

            Thread listenNewClientsThread = new Thread(this.ListenNewClients);
            Thread listenClientsInfoThread = new Thread(this.ListenClientsInfo);
            StartListenPingThread(NetworkHelper.GetLocalIPAddress(), Network.Properties.Settings.Default.PING_PORT_IN_SERVER);
            StartConnectPingThread();
            listenNewClientsThread.Start();
            listenClientsInfoThread.Start();
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public override void Stop()
        {
            _isWorking = false;
            StopConnectPingThread();
            StopListenPingThread();
            Thread.Sleep(1000);    // let worker threads finish

            // close all UdpClients
            Dispatcher.Invoke((Action)(() => _clients.Clear()));
            UpdateMulticastClients();

            base.Stop();
        }
    }
}