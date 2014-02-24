using Audio;
using log4net;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Network
{
    public class NetworkChatParticipant
    {
        protected static readonly ILog logger = LogManager.GetLogger("RadioNetwork");

        private volatile bool _receiving;
        private Thread _listenPingThread;
        private Thread _connectPingThread;
        private NamedPipeClientStream _micPipe;
        private UdpClient _streamClient;
        private Thread _streamingThread;

        /// <summary>
        /// Callback that is executed when any audio data is received.
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="data"></param>
        protected virtual void DataReceived(IPAddress addr, byte[] data) { }

        /// <summary>
        /// Machine's IP address.
        /// </summary>
        public IPAddress Addr { get; set; }

        protected NetworkChatParticipant()
        {
            _receiving = false;
            Addr = NetworkHelper.GetLocalIPAddress();
        }

        protected UdpClient InitUdpClient(int port, int timeout = 3000)
        {
            // create client
            UdpClient udpClient = new UdpClient(port);

            // set timeouts
            udpClient.Client.ReceiveTimeout = timeout;
            udpClient.Client.SendTimeout = timeout;

            // reuse ports
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            return udpClient;
        }

        private void StartReceivingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.MAX_BUFFER_SIZE];

            UdpClient client = InitUdpClient(Network.Properties.Settings.Default.AUDIO_RECEIVE_PORT);
            client.EnableBroadcast = true;
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.AUDIO_RECEIVE_PORT);

            _receiving = true;
            while (_receiving)
            {
                try
                {
                    buffer = client.Receive(ref clientEndPoint);
                }
                catch (SocketException)
                {
                    // timeout
                    continue;
                }
                DataReceived(clientEndPoint.Address, buffer);
            }
            client.Close();
        }

        protected void StartReceiving()
        {
            new Thread(StartReceivingLoop).Start();
        }

        protected void StopReceiving()
        {
            _receiving = false;
        }

        private void StartStreamingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.BUFFER_SIZE];

            // launch a thread that captures audio stream from mic and writes it to "mic" named pipe
            new Thread(() => AudioHelper.StartCapture(new UncompressedPcmChatCodec())).Start();

            // open pipe to read audio data from microphone
            _micPipe = new NamedPipeClientStream(".", "mic", PipeDirection.In);
            _micPipe.Connect();

            // read from mic and send audio data to server
            // IPEndPoint serverEndPoint = new IPEndPoint(dst, Network.Properties.Settings.Default.AUDIO_TRANSMIT_PORT);
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Broadcast, Network.Properties.Settings.Default.AUDIO_RECEIVE_PORT);
            _streamClient = InitUdpClient(Network.Properties.Settings.Default.AUDIO_TRANSMIT_PORT);
            _streamClient.EnableBroadcast = true;

            while (true)
            {
                _micPipe.Read(buffer, 0, buffer.Length);
                _streamClient.Send(buffer, buffer.Length, serverEndPoint);
            }
        }

        /// <summary>
        /// Start listening for audio stream from mic and passing it to the server.
        /// </summary>
        public void StartStreaming()
        {
            _streamingThread = new Thread(StartStreamingLoop);
            _streamingThread.Start();
        }

        /// <summary>
        /// Stop listening for audio stream from mic and passing it to the server.
        /// </summary>
        public void StopStreaming()
        {
            AudioHelper.StopCapture();
            _streamingThread.Abort();
            _micPipe.Close();
            _streamClient.Close();
        }

        protected void ListenPingThread(IPAddress PingAddr, int PING_PORT)
        {
            TcpListener listener = new TcpListener(PingAddr, PING_PORT);
            listener.Server.ReceiveTimeout = 5000;
            listener.Server.SendTimeout = 5000;
            Int32 sleepTime = 200;
            TcpClient tcpClient;
            listener.Start();
            while (true)
            {
                tcpClient = listener.AcceptTcpClient();
                Thread.Sleep(sleepTime);
            }
        }

        public void StartListenPingThread(IPAddress PingAddr, int PING_PORT)
        {
            _listenPingThread = new Thread(() => ListenPingThread(PingAddr, PING_PORT));
            _listenPingThread.Start();
        }

        public void StopListenPingThread()
        {
            _listenPingThread.Abort();
        }

        protected void StartPing(IPAddress PingAddr, int PING_PORT)
        {
            double Delta = 0.0;
            DateTime dtStart;
            do
            {
                dtStart = DateTime.Now;
                StartAsyncPing(PingAddr, PING_PORT);
                Delta = (DateTime.Now - dtStart).TotalMilliseconds;
            } while (Delta < 5000);
            throw new TimeoutException();
        }


        public void StartConnectPingThread(IPAddress PingAddr, int PING_PORT)
        {
            _connectPingThread = new Thread(() => StartPing(PingAddr, PING_PORT));
            _connectPingThread.Start();
        }

        protected bool StartAsyncPing(IPAddress PingAddr, int PING_PORT)
        {
            using (TcpClient tcp = new TcpClient())
            {
                IAsyncResult ar = tcp.BeginConnect(PingAddr, PING_PORT, null, null);
                System.Threading.WaitHandle wh = ar.AsyncWaitHandle;
                try
                {
                    if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5), false))
                    {
                        tcp.Close();
                        return false;
                    }
                    tcp.EndConnect(ar);
                }
                finally
                {
                    wh.Close();
                }
            }
            return true;
        }

        public void StopConnectPingThread()
        {
            _connectPingThread.Abort();
        }

        public void Start()
        {
            AudioHelper.StartPlaying(new UncompressedPcmChatCodec());
            StartReceiving();
        }

        public void Stop()
        {
            StopReceiving();
            AudioHelper.StopPlaying();
        }
    }
}
