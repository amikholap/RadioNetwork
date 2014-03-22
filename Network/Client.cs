using Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;
using System.IO.Pipes;

namespace Network
{
    public class Client : NetworkChatParticipant
    {
        private IPAddress _servAddr;
        private UdpClient _streamClient;
        private int pingWaitReply = 2500;

        /// <summary>
        /// Client's callsign.
        /// </summary>
        public string Callsign { get; set; }
        /// <summary>
        /// Receive frequency.
        /// </summary>
        public UInt32 Fr { get; set; }
        /// <summary>
        /// Transmit frequency.
        /// </summary>
        public UInt32 Ft { get; set; }
        /// <summary>
        /// IP multicast group where the client will send audio data.
        /// </summary>
        public IPAddress TransmitMulticastGroupAddr { get; set; }
        /// <summary>
        /// IP multicast group where the client will listen for audio data.
        /// </summary>
        public IPAddress ReceiveMulticastGroupAddr { get; set; }


        public event EventHandler<EventArgs> ServerQuit;


        public Client(string callsign, UInt32 fr, UInt32 ft)
            : base()
        {
            Callsign = callsign;
            Fr = fr;
            Ft = ft;
            UpdateMulticastAddrs();
        }

        public Client(IPAddress addr, string callsign, UInt32 fr, UInt32 ft)
            : this(callsign, fr, ft)
        {
            Addr = addr;
        }

        protected virtual void OnServerQuit(EventArgs e)
        {
            // a temporary variable is required to keep it thread-safe
            var temp = ServerQuit;
            if (temp != null)
                temp(this, e);
        }

        protected void UpdateMulticastAddrs()
        {
            TransmitMulticastGroupAddr = NetworkHelper.FreqToMcastGroup(Ft);
            ReceiveMulticastGroupAddr = NetworkHelper.FreqToMcastGroup(Fr);
        }

        /// <summary>
        /// Return a list of available server IP addresses.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPAddress> DetectServers()
        {
            Byte[] dgram = new byte[256];
            List<IPAddress> serverIPs = new List<IPAddress>();

            UdpClient udpClient = NetworkHelper.InitUdpClient(Network.Properties.Settings.Default.BROADCAST_PORT);
            udpClient.EnableBroadcast = true;
            // determine port && BroadcastAddr
            IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, Network.Properties.Settings.Default.BROADCAST_PORT);
            IPEndPoint anyEndPoint = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.BROADCAST_PORT);

            // send BroadCast message "client" in byte[]
            string request = "client";
            dgram = Encoding.UTF8.GetBytes(request);
            // Blocks until a message returns on this socket from a remote host.
            udpClient.Send(dgram, dgram.Length, broadcastEndPoint);

            // listen for server's reponse for 5 seconds
            DateTime t = DateTime.Now;
            try
            {
                // wait for server's response for 5 seconds
                while ((DateTime.Now - t) < TimeSpan.FromSeconds(5))
                {
                    dgram = udpClient.Receive(ref anyEndPoint);
                    string response = Encoding.UTF8.GetString(dgram);
                    // data is in format {client,server}
                    if (response == "server")
                    {
                        serverIPs.Add(anyEndPoint.Address);
                        logger.Debug(String.Format("Found server: {0}", anyEndPoint.Address));
                    }
                }
            }
            catch (SocketException)
            {
                // timeout
            }
            catch (Exception e)
            {
                logger.Error("Unhandled exception while detecting servers.", e);
            }
            finally
            {
                udpClient.Close();
            }

            return serverIPs;
        }

        /// <summary>
        /// Send to server updated info about this client to server.
        /// </summary>
        public void UpdateClientInfo()
        {
            Byte[] dgram = new Byte[256];
            IPEndPoint ipEndPoint = new IPEndPoint(_servAddr, Network.Properties.Settings.Default.TCP_PORT);
            TcpClient tcpClient = new TcpClient();

            try
            {
                tcpClient.Connect(ipEndPoint);

                string message = String.Format("UPDATE\n{0}\n{1},{2}", Callsign, Fr, Ft);
                dgram = System.Text.Encoding.UTF8.GetBytes(message);
                using (NetworkStream ns = tcpClient.GetStream())
                {
                    ns.Write(dgram, 0, dgram.Length);
                }
            }
            catch (Exception e)
            {
                logger.Error("Unhandled exception while sending client's info.", e);
                return;
            }
            finally
            {
                tcpClient.Close();
            }
        }

        /// <summary>
        /// Additionally prepare a UDP connection to the server.
        /// </summary>
        public override void PrepareStreaming()
        {
            _streamClient = NetworkHelper.InitUdpClient(Network.Properties.Settings.Default.SERVER_AUDIO_PORT);
            base.PrepareStreaming();
        }

        /// <summary>
        /// Close UDP connection to the server.
        /// </summary>
        public override void StopStreaming()
        {
            base.StopStreaming();
            _streamClient.Close();
        }

        protected void StartPing(IPAddress PingAddr, int PING_PORT)
        {
            double Delta = 0;
            bool th = false;
            DateTime dtStart;
            base._connectPing = true;
            while (base._connectPing == true)
            {
                dtStart = DateTime.Now;
                th = StartAsyncPing(PingAddr, PING_PORT);
                Delta = (DateTime.Now - dtStart).TotalMilliseconds;
                if (Delta < pingWaitReply || th == true)
                {
                    Thread.Sleep(pingWaitReply - (int)Delta);
                }
                else
                {
                    throw new System.ArgumentException("Server is not not responding", "Ping server");
                }
            }
        }

        public void StartConnectPingThread(IPAddress PingAddr, int PING_PORT)
        {
            base._connectPingThread = new Thread(() => StartPing(PingAddr, PING_PORT));
            base._connectPingThread.Start();
        }

        public void StopConnectPingThread()
        {
            base._connectPing = false;
        }

        /// <summary>
        /// Start capturing audio from mic and send to the server.
        /// </summary>
        protected override void StartStreamingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.BUFFER_SIZE];

            if (_servAddr == null)
            {
                return;
            }
            IPEndPoint serverEndPoint = new IPEndPoint(_servAddr, Network.Properties.Settings.Default.SERVER_AUDIO_PORT);

            while (true)
            {
                _micPipe.Read(buffer, 0, buffer.Length);
                _streamClient.Send(buffer, buffer.Length, serverEndPoint);
            }
        }

        /// <summary>
        /// Start receiving audio from a multicast address derived from Fr and send it to player buffer.
        /// </summary>
        protected override void StartReceivingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.MAX_BUFFER_SIZE];

            UdpClient client = NetworkHelper.InitUdpClient();
            client.JoinMulticastGroup(ReceiveMulticastGroupAddr);

            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.MULTICAST_PORT);
            client.Client.Bind(localEP);

            _receiving = true;
            while (_receiving)
            {
                try
                {
                    buffer = client.Receive(ref localEP);
                }
                catch (SocketException)
                {
                    // timeout
                    continue;
                }

                // add received data to the player queue
                AudioHelper.AddSamples(buffer);
            }
            client.Close();
        }

        /// <summary>
        /// Connect to a server and start streaming audio.
        /// </summary>
        public void Start(IPAddress serverAddr)
        {
            _servAddr = serverAddr;
            UpdateClientInfo();
            base.Start();
            StartListenPingThread(NetworkHelper.GetLocalIPAddress(), Network.Properties.Settings.Default.PING_PORT_OUT_SERVER);
            StartConnectPingThread(_servAddr, Network.Properties.Settings.Default.PING_PORT_IN_SERVER);
        }

        /// <summary>
        /// Stop client.
        /// </summary>
        public override void Stop()
        {
            base.Stop();
            StopConnectPingThread();
            StopListenPingThread();
            Thread.Sleep(1000);    // let worker threads finish
        }
    }
}
