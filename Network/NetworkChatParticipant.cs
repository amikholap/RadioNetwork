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

        private int pingWaitAccept = 6000;
        protected INetworkChatCodec _codec;

        private volatile bool _listenPing;
        protected volatile bool _muted;
        protected volatile bool _connectPing;
        protected volatile bool _receiving;

        private Thread _listenPingThread;
        protected Thread _connectPingThread;

        /// <summary>
        /// Machine's IP address.
        /// </summary>
        public IPAddress Addr { get; set; }

        protected virtual void StartSendPingLoop() { }
        protected virtual void StartReceivingLoop() { }

        protected virtual void audio_OutputDataAvailable(object sender, AudioIOEventArgs e) { }
        protected virtual void audio_InputDataAvailable(object sender, AudioIOEventArgs e)
        {
            // add the chunk to the playback buffer
            if (e.Item != null)
            {
                AudioHelper.AddSamples(e.Item.Data);
            }
        }

        protected NetworkChatParticipant()
        {
            _connectPing = false;
            _listenPing = false;
            _receiving = false;
            _listenPing = false;
            _muted = false;

            Addr = NetworkHelper.GetLocalIPAddress();
            _codec = new UncompressedPcmChatCodec();
        }

        protected void StartLoggingAudio()
        {
            string historyDir = Path.Combine(Directory.GetCurrentDirectory(), "history");
            {
                Directory.CreateDirectory(historyDir);
            }
            string filename = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss") + ".wav";
            string filepath = Path.Combine(historyDir, filename);

            AudioHelper.StartLogging(filepath, _codec);
        }

        protected void StopLoggingAudio()
        {
            AudioHelper.StopLogging();
        }

        protected void StartListenPingLoop(IPAddress PingAddr, int PING_PORT)
        {
            logger.Debug("ping listen from " + IPAddress.Parse(PingAddr.ToString()) + "port " + PING_PORT);
            TcpListener listener = new TcpListener(PingAddr, PING_PORT);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            TcpClient tcpClient;
            listener.Server.ReceiveTimeout = 60000;
            listener.Server.SendTimeout = 60000;
            listener.Start();
            _listenPing = true;
            while (_listenPing)
            {
                // Step 0: Client connection
                if (listener.Pending())
                {
                    tcpClient = listener.AcceptTcpClient();
                    logger.Debug(String.Format("ping listen accept SYN send from {0}", tcpClient.Client.RemoteEndPoint));
                    tcpClient.Close();
                }
                else
                    Thread.Sleep(pingWaitAccept);
            }
            listener.Stop();
            logger.Debug("ping listen accept stop");
        }

        protected void StartListenPing()
        {
            if (this is Server)
                _listenPingThread = new Thread(() => StartListenPingLoop(Addr, Network.Properties.Settings.Default.PING_PORT_IN_SERVER));
            else
                _listenPingThread = new Thread(() => StartListenPingLoop(Addr, Network.Properties.Settings.Default.PING_PORT_OUT_SERVER));
            _listenPingThread.Start();
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
                WaitHandle wh = ar.AsyncWaitHandle;
                try
                {
                    logger.Debug("ping async " + IPAddress.Parse(PingAddr.ToString()) + " start");
                    if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(pingWaitAccept), false))
                    {
                        logger.Debug("ping async " + IPAddress.Parse(PingAddr.ToString()) + " failed");
                        tcp.Close();
                        return false;
                    }
                    tcp.EndConnect(ar);
                }
                catch (SocketException)
                {
                    logger.Debug("ping async " + IPAddress.Parse(PingAddr.ToString()) + " SocketException");
                }
                finally
                {
                    wh.Close();
                }
            }
            logger.Debug("ping async " + IPAddress.Parse(PingAddr.ToString()) + " successful");
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
        /// Stop playing any incoming audio data.
        /// </summary>
        public void Mute()
        {
            _muted = true;
        }
        /// <summary>
        /// Resume recording incoming audio data.
        /// </summary>
        public void UnMute()
        {
            _muted = false;
        }

        /// <summary>
        /// Start capturing data from mic.
        /// </summary>
        public virtual void StartStreaming()
        {
            AudioHelper.StartCapture(_codec);
        }
        /// <summary>
        /// Stop capturing data from mic.
        /// </summary>
        public virtual void StopStreaming()
        {
            AudioHelper.StopCapture();
        }

        public virtual void Start()
        {
            StartLoggingAudio();
            AudioHelper.StartPlaying(new UncompressedPcmChatCodec());
            StartReceiving();
            AudioIO.InputTick += audio_InputDataAvailable;
            AudioIO.OutputTick += audio_OutputDataAvailable;
            StartListenPing();
            StartSendPing();
        }
        public virtual void Stop()
        {
            AudioIO.InputTick -= audio_InputDataAvailable;
            AudioIO.OutputTick -= audio_OutputDataAvailable;
            StopSendPing();
            StopListenPing();
            StopReceiving();
            AudioHelper.StopPlaying();
            StopLoggingAudio();
        }
    }
}
