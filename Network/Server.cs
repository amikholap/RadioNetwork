using Audio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

        /// <summary>
        /// Details about the current talker.
        /// </summary>
        private ClientActivity _lastTalked;

        private int pingSendWaitTimeOut = 5000;

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

        public event EventHandler<TalkerChangedEventArgs> TalkerChanged;

        public Server()
            : base()
        {
            _isWorking = true;
            _clients = new ObservableCollection<Client>();
            _mcastClients = new Dictionary<IPAddress, UdpClient>();
            _lastTalked = new ClientActivity(null, DateTime.MinValue);
        }

        protected void OnTalkerChanged(TalkerChangedEventArgs e)
        {
            if (TalkerChanged != null)
            {
                TalkerChanged(this, e);
            }
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
                                int a = 0;
                                foreach(Client item in Clients)
                                {
                                    if (String.Compare(callsign, item.Callsign) == 0)
                                    {
                                        a = 1;
                                    }
                                }
                                if (a == 0) // client callsign is free
                                {
                                    String message = "free";
                                    Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);
                                    // send data 'free' to client
                                    ns.Write(data, 0, data.Length);
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
                                }
                                else        // client callsign is busy
                                {
                                    String message = "busy";
                                    Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);
                                    // send data 'busy' to client
                                    ns.Write(data, 0, data.Length);
                                }     
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

        protected override void StartSendPingLoop()
        {
            int TimeOut = pingSendWaitTimeOut;
            base._connectPing = true;
            while (base._connectPing == true)
            {
                foreach (Client client in _clients.ToArray())
                {
                    if (base.StartAsyncPing(client.Addr, Network.Properties.Settings.Default.PING_PORT_OUT_SERVER) == false)
                    {
                        logger.Debug("ping remove " + IPAddress.Parse(client.Addr.ToString()));
                        Dispatcher.Invoke((Action)(() => _clients.Remove(client)));  // remove non-ping client
                    }
                }
                Thread.Sleep(TimeOut);
            }
        }

        /// <summary>
        /// Update UDPClients for multicast groups.
        /// </summary>
        private void UpdateMulticastClients()
        {
            // already initialized multicast groups
            var currentAddrs = _mcastClients.Keys;

            // get all disctinct multicast addresses for transmitting
            // and receiving and update the list of UdpClients
            var trAddrs = _clients.Select(c => c.TransmitMulticastGroupAddr).Distinct();
            var recAddrs = _clients.Select(c => c.ReceiveMulticastGroupAddr).Distinct();
            var newAddrs = trAddrs.Union(recAddrs).ToList();

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

        /// <summary>
        /// Take a chunk from the output queue and send it
        /// to the input queue with server context.
        /// This will set the highest priority.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void audio_OutputDataAvailable(object sender, AudioIOEventArgs e)
        {
            if (e.Item == null)
            {
                return;
            }

            _lastTalked.Timestamp = e.Item.Timestamp;
            if (_lastTalked.Talker != this)
            {
                _lastTalked.Talker = this;
                OnTalkerChanged(new TalkerChangedEventArgs(_lastTalked.Talker));
            }

            // interrupt any current speaker and send server message to all clients
            foreach (IPAddress mcastAddr in _mcastClients.Keys)
            {
                try
                {
                    _mcastClients[mcastAddr].Send(e.Item.Data, e.Item.Data.Length, new IPEndPoint(mcastAddr, Network.Properties.Settings.Default.MULTICAST_PORT));
                }
                catch (KeyNotFoundException)
                {
                }
            }
        }

        /// <summary>
        /// Receive data from self or a single client at a time and route it to mcast groups.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void audio_InputDataAvailable(object sender, AudioIOEventArgs e)
        {
            // drop last talked lock after 0.3s of inactivity
            if ((DateTime.Now - _lastTalked.Timestamp).Milliseconds > 300 && _lastTalked.Talker != null)
            {
                _lastTalked.Talker = null;
                OnTalkerChanged(new TalkerChangedEventArgs(null));
            }

            if (e.Item == null)
            {
                return;
            }

            // ensure that only clients can produce input data
            Debug.Assert(e.Item.Context is Client);

            // check that either no one talked last time
            // or it was the client from e.Item.Context
            if (_lastTalked.Talker == null)
            {
                _lastTalked.Talker = (Client)e.Item.Context;
                _lastTalked.Timestamp = e.Item.Timestamp;
                OnTalkerChanged(new TalkerChangedEventArgs(_lastTalked.Talker));
            }
            if (_lastTalked.Talker != e.Item.Context)
            {
                return;
            }

            // spread the message to other clients with that freq
            IPAddress mcastAddr = ((Client)_lastTalked.Talker).TransmitMulticastGroupAddr;
            try
            {
                _mcastClients[mcastAddr].Send(e.Item.Data, e.Item.Data.Length, new IPEndPoint(mcastAddr, Network.Properties.Settings.Default.MULTICAST_PORT));
            }
            catch (KeyNotFoundException)
            {
                // this client may already disconnect
            }

            base.audio_InputDataAvailable(sender, e);
        }

        /// <summary>
        /// Receive audio data from clients and add it to the input queue.
        /// </summary>
        protected override void StartReceivingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.BUFFER_SIZE];

            IPEndPoint clientEP = null;
            UdpClient udpClient = NetworkHelper.InitUdpClient(new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.SERVER_AUDIO_PORT));
            Client client;

            _receiving = true;
            while (_receiving)
            {
                while (udpClient.Available == 0)
                {
                    Thread.Sleep(100);
                }
                buffer = udpClient.Receive(ref clientEP);

                client = _clients.FirstOrDefault(c => c.Addr.Equals(clientEP.Address));
                if (client != null)
                {
                    AudioIO.AddInputData(buffer, client);
                }
            }
            udpClient.Close();
        }

        public override void StartStreaming()
        {
            Mute();
            base.StartStreaming();
        }
        public override void StopStreaming()
        {
            base.StopStreaming();
            UnMute();
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
            listenNewClientsThread.Start();
            listenClientsInfoThread.Start();
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public override void Stop()
        {
            _isWorking = false;
            Thread.Sleep(1000);    // let worker threads finish

            // close all UdpClients
            Dispatcher.Invoke((Action)(() => _clients.Clear()));
            UpdateMulticastClients();

            base.Stop();
        }
    }
}