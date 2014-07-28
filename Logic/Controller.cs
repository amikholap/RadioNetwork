using Audio;
using Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using System.Net;
using System.Threading;
using System.Diagnostics;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]


namespace Logic
{
    public static class Controller
    {
        #region Properties

        private static Client _client;
        private static Server _server;

        private static ILog logger = LogManager.GetLogger("RadioNetwork");

        public static ControllerMode Mode { get; private set; }

        public static Client Client
        {
            get { return _client; }
        }

        public static Server Server
        {
            get { return _server; }
        }

        /// <summary>
        /// Information about active servers in local network.
        /// 
        /// Functional only in client mode.
        /// </summary>
        public static IEnumerable<ServerSummary> AvailableServers { get; set; }

        #endregion

        #region Constructors

        static Controller()
        {
            _client = new Client("Тополь", 255, 255);
            _server = new Server("Береза");
            Mode = ControllerMode.None;

            SpeechRecognizer.SpeechRecognized += SpeechRecognizer_SpeechRecognized;

            // Update available servers every second
            Thread detectServersThread = new Thread(() =>
            {
                TimeSpan sleepTime;
                TimeSpan interval = TimeSpan.FromSeconds(3);
                DateTime lastUpdated = DateTime.Now;
                while (true)
                {
                    // Don't update AvailableServers in server mode
                    if (Mode != ControllerMode.Server)
                    {
                        int nServers = 0;
                        if (AvailableServers != null)
                        {
                            nServers = AvailableServers.Count();
                        }

                        // Update a list of available servers.
                        // Check twice if any server disappears.
                        // Use a temporary variable to avoid UI update.
                        IEnumerable<ServerSummary> availableServers;
                        availableServers = Network.Client.DetectServers();
                        if (availableServers.Count() < nServers)
                        {
                            availableServers = Network.Client.DetectServers();
                        }
                        if (availableServers.Count() < nServers)
                        {
                        }
                        AvailableServers = availableServers.OrderBy(ss => ss.Addr.ToString());
                    }

                    sleepTime = interval - (DateTime.Now - lastUpdated);
                    if (sleepTime > TimeSpan.Zero)
                    {
                        Thread.Sleep(sleepTime);
                    }
                    lastUpdated = DateTime.Now;
                }
            });
            detectServersThread.IsBackground = true;  // don't wait this thread to terminate on exit
            detectServersThread.Start();
        }

        #endregion

        #region Events

        public static event EventHandler<Logic.SpeechRecognizedEventArgs> SpeechRecognized;
        private static void OnSpeechRecognized(Logic.SpeechRecognizedEventArgs e)
        {
            if (SpeechRecognized != null)
            {
                SpeechRecognized(null, e);
            }
        }

        #endregion

        #region EventHandlers

        private static void Client_ServerDisconnected(object sender, ClientEventArgs e)
        {
            _client.Dispatcher.Invoke(new Action(() => { throw new RNException(e.Message); }), null);
        }

        private static void SpeechRecognizer_SpeechRecognized(object sender, Network.SpeechRecognizedEventArgs e)
        {
            OnSpeechRecognized(new Logic.SpeechRecognizedEventArgs(e.Talker, e.Message));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initialize internal services.
        /// </summary>
        public static void Init()
        {
            AudioIO.StartTicking();
        }
        /// <summary>
        /// Shutdown internal services.
        /// </summary>
        public static void ShutDown()
        {
            AudioIO.StopTicking();
        }

        /// <summary>
        /// Run application in client mode.
        /// </summary>
        /// <param name="callsign"></param>
        /// <param name="fr"></param>
        /// <param name="ft"></param>
        public static bool StartClient(string callsign, UInt32 fr, UInt32 ft, IPAddress servAddr)
        {
            string reply = String.Empty;

            if (Mode == ControllerMode.Client && _client.ServAddr != null)
            {
                reply = _client.UpdateClientInfo(callsign, fr, ft);
            }
            else
            {
                // stop any existing process
                Stop();

                // create Client instance
                _client = new Client(callsign, fr, ft);
                _client.ServerDisconnected += Client_ServerDisconnected;

                reply = _client.Start(servAddr);
            }

            switch (reply)
            {
                case "free":
                    {
                        _client.Callsign = callsign;
                        _client.Fr = fr;
                        _client.Ft = ft;
                        Mode = ControllerMode.Client;
                        return true;
                    }
                case "busy":
                    {
                        _client.OnClientEvent(new ClientEventArgs("Позывной уже используется, задайте другой позывной"));
                        return false;
                    }
                default:
                    {
                        _client.OnClientEvent(new ClientEventArgs("Подключение невозможно, некорректный ответ от сервера"));
                        return false;
                    }
            }
        }

        /// <summary>
        /// Run application in server mode.
        /// </summary>
        public static void StartServer(string callsign)
        {
            if (Mode != ControllerMode.Server)
            {
                // stop any existing non server processes
                Stop();

                // create Server instance
                _server = new Server(callsign);

                // try to start server
                try
                {
                    _server.Start();
                    Mode = ControllerMode.Server;
                }
                catch (Exception e)
                {
                    Mode = ControllerMode.None;
                    logger.Error("Unhandled exception while starting server.", e);
                    return;
                }
            }
        }

        /// <summary>
        /// Stop application.
        /// </summary>
        public static void Stop()
        {
            switch (Mode)
            {
                case ControllerMode.Client:
                    if (_client.ServAddr != null)
                    {
                        _client.UpdateClientInfo(_client.Callsign, _client.Fr, _client.Ft, "DELETE", 0);
                        _client.Stop();
                        _client.ServerDisconnected -= Client_ServerDisconnected;
                    }
                    break;
                case ControllerMode.Server:
                    _server.Stop();
                    break;
            }
            Mode = ControllerMode.None;
        }

        public static void StartTalking()
        {
            switch (Mode)
            {
                case ControllerMode.Client:
                    _client.StartStreaming();
                    break;
                case ControllerMode.Server:
                    _server.StartStreaming();
                    break;
            }
        }

        public static void StopTalking()
        {
            switch (Mode)
            {
                case ControllerMode.Client:
                    _client.StopStreaming();
                    break;
                case ControllerMode.Server:
                    _server.StopStreaming();
                    break;
            }
        }

        #endregion
    }
}
