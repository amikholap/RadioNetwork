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

namespace Network
{
    public class NetworkChatParticipant : DispatcherObject
    {
        protected static readonly ILog logger = LogManager.GetLogger("RadioNetwork");

        protected volatile bool _receiving;

        private Thread _streamingThread;

        protected NamedPipeClientStream _micPipe;

        private volatile bool _listenPing;
        protected bool _connectPing;
        private Thread _listenPingThread;
        protected Thread _connectPingThread;
        private int pingWaitAccept = 1000;

        protected virtual void StartStreamingLoop() { }
        protected virtual void StartReceivingLoop() { }

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
            _connectPing = false;
            _listenPing = false;
            _receiving = false;
            _listenPing = false;
            Addr = NetworkHelper.GetLocalIPAddress();
        }
        protected void ListenPingThread(IPAddress PingAddr, int PING_PORT)
        {
            TcpListener listener = new TcpListener(PingAddr, PING_PORT);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            TcpClient tcpClient;
            listener.Server.ReceiveTimeout = 50000;
            listener.Server.SendTimeout = 100;
            listener.Start();
            _listenPing = true;
            while (_listenPing)
            {
                // Step 0: Client connection
                tcpClient = listener.AcceptTcpClient();
                tcpClient.Close();
            }
            listener.Stop();
        }

        public void StartListenPingThread(IPAddress PingAddr, int PING_PORT)
        {
            _listenPingThread = new Thread(() => ListenPingThread(PingAddr, PING_PORT));
            _listenPingThread.Start();
        }

        public void StopListenPingThread()
        {
            _listenPing = false;
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
        public virtual void PrepareStreaming()
        {
            // capture audio stream from mic and write it to "mic" named pipe
            AudioHelper.StartCapture(new UncompressedPcmChatCodec());

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
        }

        public virtual void Stop()
        {
            StopReceiving();
            AudioHelper.StopPlaying();
        }
    }
}
