using Audio;
using System;
using System.Collections.Generic;
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
        private List<Client> _clients;

        private UdpClient _multicastClient;

        public Server()
            : base()
        {
            _isWorking = true;
            _clients = new List<Client>();

            // initialize an UdpClient to send data to a predefined IP multicast group
            _multicastClient = new UdpClient();
            _multicastClient.JoinMulticastGroup(_multicastEP.Address);
        }

        /// <summary>
        /// Listen on BROADCAST_PORT for any udp datagrams with client ip addresses.
        /// Reply with a string "server"
        /// </summary>
        private void ListenNewClients()
        {
            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.BROADCAST_PORT);
            UdpClient client = InitUdpClient(Network.Properties.Settings.Default.BROADCAST_PORT);
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
                        int fr, ft;              // client's receive and transmit frequencies

                        var buf = new byte[100];
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
                                    fr = int.Parse(lines[2].Split(',')[0]);
                                    ft = int.Parse(lines[2].Split(',')[1]);
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
                                if (!_clients.Exists(c => c.Addr == clientAddr))
                                {
                                    _clients.Add(new Client(clientAddr, callsign, fr, ft));
                                    logger.Debug(String.Format("Client connected: {0} with freqs {1}, {2}", callsign, fr, ft));
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

        protected override void StartStreamingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.BUFFER_SIZE];

            // launch a thread that captures audio stream from mic and writes it to "mic" named pipe
            new Thread(() => AudioHelper.StartCapture(new UncompressedPcmChatCodec())).Start();

            // open pipe to read audio data from microphone
            _micPipe = new NamedPipeClientStream(".", "mic", PipeDirection.In);
            _micPipe.Connect();

            // read from mic and send audio data to the multicast group
            _streamClient = InitUdpClient(_multicastEP.Port);

            while (true)
            {
                _micPipe.Read(buffer, 0, buffer.Length);
                _streamClient.Send(buffer, buffer.Length, _multicastEP);
            }
        }

        protected override void StartReceivingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.MAX_BUFFER_SIZE];

            IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.SERVER_AUDIO_PORT);
            UdpClient client = InitUdpClient(clientEP.Port);

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

                // add received data to the player queue
                AudioHelper.AddSamples(buffer);

                _multicastClient.Send(buffer, buffer.Length);
            }
            client.Close();
        }

        /// <summary>
        /// Launch the server.
        /// It spawns several threads for listening and processing.
        /// client messages.
        /// </summary>
        public void Start()
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
        public void Stop()
        {
            base.Stop();

            _isWorking = false;
            Thread.Sleep(1000);    // let worker threads finish
        }
    }
}