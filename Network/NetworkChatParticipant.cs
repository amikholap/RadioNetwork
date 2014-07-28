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
using System.ComponentModel;

namespace Network
{
    public class NetworkChatParticipant : DispatcherObject, INotifyPropertyChanged
    {
        #region Properties

        protected static readonly ILog logger = LogManager.GetLogger("RadioNetwork");

        private int pingWaitAccept = 6000;
        protected INetworkChatCodec _codec;

        protected volatile bool _listenPing;
        protected volatile bool _muted;
        protected volatile bool _connectPing;
        protected volatile bool _receiving;
        protected volatile bool _isWorking;

        private Thread _listenPingThread;
        protected Thread _connectPingThread;

        /// <summary>
        /// Machine's IP address.
        /// </summary>
        public IPAddress Addr { get; set; }

        /// <summary>
        /// Callsign of the radionetwork participant.
        /// </summary>
        public string Callsign { get; set; }

        /// <summary>
        /// Tell working state.
        /// True by default.
        /// When set to false a shutdown will follow in several seconds.
        /// </summary>
        public bool IsWorking
        {
            get
            {
                return _isWorking;
            }
            private set
            {
                _isWorking = value;
                NotifyPropertyChanged("IsWorking");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Constructors

        protected NetworkChatParticipant(string callsign)
        {
            _connectPing = false;
            _listenPing = false;
            _receiving = false;
            _listenPing = false;
            _muted = false;
            _codec = new UncompressedPcmChatCodec();

            Addr = NetworkHelper.GetLocalIPAddress();
            Callsign = callsign;
        }

        #endregion

        #region EventHandlers

        protected void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected virtual void AudioIO_OutputDataAvailable(object sender, AudioIOEventArgs e) { }
        protected virtual void AudioIO_InputDataAvailable(object sender, AudioIOEventArgs e)
        {
            // add the chunk to the playback buffer
            if (e.Item != null)
            {
                AudioHelper.AddSamples(e.Item.Data);
            }
        }

        #endregion

        #region Methods

        protected virtual void StartSendPingLoop() { }
        protected virtual void StartReceivingLoop() { }

        /// <summary>
        /// Return absolute path to file in log dir with now timestamp as name.
        /// The file isn't created.
        /// </summary>
        /// <param name="extension"></param>
        /// <returns></returns>
        protected string BuildLogFilePath(string extension)
        {
            string logDir = Path.Combine(Directory.GetCurrentDirectory(), "log");
            {
                Directory.CreateDirectory(logDir);
            }
            string fileName = DateTime.Now.ToString(Network.Properties.Settings.Default.TIMESTAMP_FORMAT) + "." + extension;
            string filePath = Path.Combine(logDir, fileName);
            return filePath;
        }

        protected void StartLoggingAudio()
        {
            string logFilePath = BuildLogFilePath("wav");
            AudioHelper.StartLogging(logFilePath, _codec);
        }

        protected void StopLoggingAudio()
        {
            AudioHelper.StopLogging();
        }

        protected void StartListenPingLoop(IPAddress PingAddr, int PING_PORT)
        {
            logger.Debug("ping listen from " + IPAddress.Parse(PingAddr.ToString()) + " port " + PING_PORT);
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
                {
                    Thread.Sleep(pingWaitAccept);
                }
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
                    if (ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(pingWaitAccept), true) == false)
                    {
                        logger.Debug("ping async " + IPAddress.Parse(PingAddr.ToString()) + " failed");
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
                    tcp.Close();
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
            IsWorking = true;
            StartLoggingAudio();
            AudioHelper.StartPlaying(new UncompressedPcmChatCodec());
            StartReceiving();
            AudioIO.InputTick += AudioIO_InputDataAvailable;
            AudioIO.OutputTick += AudioIO_OutputDataAvailable;
            StartListenPing();
            StartSendPing();
        }
        public virtual void Stop()
        {
            AudioIO.InputTick -= AudioIO_InputDataAvailable;
            AudioIO.OutputTick -= AudioIO_OutputDataAvailable;
            StopSendPing();
            StopListenPing();
            StopReceiving();
            AudioHelper.StopPlaying();
            StopLoggingAudio();
            IsWorking = false;
            Thread.Sleep(1000);    // let worker threads finish
        }

        #endregion
    }
}
