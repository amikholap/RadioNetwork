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
using System.Windows.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Network
{
    public class NetworkChatParticipant : DispatcherObject
    {
        protected static readonly ILog logger = LogManager.GetLogger("RadioNetwork");

        private int pingWaitAccept = 8000;
        protected INetworkChatCodec _codec;
        protected NamedPipeClientStream _micPipe;

        private volatile bool _listenPing;
        protected volatile bool _connectPing;
        protected volatile bool _receiving;

        private Thread _streamingThread;
        private Thread _listenPingThread;
        protected Thread _connectPingThread;

        protected virtual void StartSendPingLoop() { }
        protected virtual void StartStreamingLoop() { }
        protected virtual void StartReceivingLoop() { }

        /// <summary>
        /// Machine's IP address.
        /// </summary>
        public IPAddress Addr { get; set; }

        protected NetworkChatParticipant()
        {
            _connectPing = false;
            _listenPing = false;
            _receiving = false;
            _listenPing = false;
            Addr = NetworkHelper.GetLocalIPAddress();

            _codec = new UncompressedPcmChatCodec();

            InitWavFile();
        }

        protected void InitWavFile()
        {
            string historyDir = Path.Combine(Directory.GetCurrentDirectory(), "history");
            if (!Directory.Exists(historyDir))
            {
                Directory.CreateDirectory(historyDir);
            }
            string filename = DateTime.Now.ToString("yyyy/MM/dd-HH/mm/ss") + ".wav";
            string filepath = Path.Combine(historyDir, filename);
            FileStream f = File.Create(filepath);
        }

        protected void StartListenPingLoop(IPAddress PingAddr, int PING_PORT)
        {
            TcpListener listener = new TcpListener(PingAddr, PING_PORT);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            TcpClient tcpClient;
            listener.Server.ReceiveTimeout = 30000;
            listener.Server.SendTimeout = 30000;
            listener.Start();
            _listenPing = true;
            while (_listenPing)
            {
                // Step 0: Client connection
                if (listener.Pending())
                {
                    tcpClient = listener.AcceptTcpClient();
                    tcpClient.Close();
                }
            }
            listener.Stop();
        }

        protected void StartListenPing()
        {
            _listenPingThread = new Thread(() => StartListenPingLoop(Addr, Network.Properties.Settings.Default.PING_PORT_IN_SERVER));
            _listenPingThread.Start();
        }

        /// <summary>
        /// Process received data.
        /// </summary>
        /// <param name="data"></param>
        protected void OnDataReceived(byte[] data)
        {
            // add the data to the player queue
            AudioHelper.AddSamples(data);
        }

        protected void StartSendPing()
        {
            _connectPingThread = new Thread(() => StartSendPingLoop());
            _connectPingThread.Start();
        }

        protected void StopListenPing()
        {
            _listenPing = false;
        }

        protected void StopSendPing()
        {
            _connectPing = false;
        }

        protected bool StartAsyncPing(IPAddress PingAddr, int PING_PORT)
        {
            using (TcpClient tcp = new TcpClient())
            {
                IAsyncResult ar = tcp.BeginConnect(PingAddr, PING_PORT, null, null);
                System.Threading.WaitHandle wh = ar.AsyncWaitHandle;
                try
                {
                    if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(pingWaitAccept), false))
                    {
                        tcp.Close();
                        return false;
                    }
                    tcp.EndConnect(ar);
                }
                catch (SocketException)
                {
                }
                finally
                {
                    wh.Close();
                }
            }
            return true;
        }

        protected void StartReceiving()
        {
            new Thread(StartReceivingLoop).Start();
        }

        protected void StopReceiving()
        {
            _receiving = false;
        }

        /// <summary>
        /// Initialize UDP client and mic pipe and start capturing audio.
        /// </summary>
        protected virtual void PrepareStreaming()
        {
            // capture audio stream from mic and write it to "mic" named pipe
            AudioHelper.StartCapture(_codec);

            // open pipe to read audio data from microphone
            _micPipe = new NamedPipeClientStream(".", "mic", PipeDirection.In);
            _micPipe.Connect();
        }

        /// <summary>
        /// Start listening for audio stream from mic and passing it to the server.
        /// </summary>
        public void StartStreaming()
        {
            PrepareStreaming();
            _streamingThread = new Thread(StartStreamingLoop);
            _streamingThread.Start();
            AudioHelper.Mute();
        }

        public virtual void StopStreaming()
        {
            AudioHelper.UnMute();
            AudioHelper.StopCapture();
            _streamingThread.Abort();
            _micPipe.Close();
        }

        public virtual void Start()
        {
            AudioHelper.StartPlaying(new UncompressedPcmChatCodec());
            StartReceiving();
            StartListenPing();
            StartSendPing();
        }

        public virtual void Stop()
        {
            StopSendPing();
            StopListenPing();
            StopReceiving();
            AudioHelper.StopPlaying();
        }
    }
}
