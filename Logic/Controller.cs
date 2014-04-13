using Audio;
using Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]


namespace Logic
{
    public static class Controller
    {
        private static Client _client;
        private static Server _server;

        private static ILog logger = LogManager.GetLogger("RadioNetwork");

        public static ControllerMode Mode { get; set; }

        public static Client Client
        {
            get { return _client; }
        }

        public static Server Server
        {
            get { return _server; }
        }

        static void Client_ServerDisconnected(object sender, ServerDisconnectedEventArgs e)
        {
            _client.Dispatcher.Invoke(new Action(() => { throw new RNException(e.Message); }), null);
        }

        static Controller()
        {
            _client = null;
            _server = null;
            Mode = ControllerMode.None;
        }

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
        public static void StartClient(string callsign, UInt32 fr, UInt32 ft)
        {
            if (Mode != ControllerMode.Client)
            {
                // stop any existing process
                Stop();

                // create Client instance
                _client = new Client(callsign, fr, ft);
                _client.ServerDisconnected += Client_ServerDisconnected;
                // find a server and connect to it
                var servers = _client.DetectServers().ToList();
                if (servers.Count == 0)
                {
                    throw new RNException("Сервер не найден");
                }
                else
                {
                    _client.Start(servers[0]);
                    Mode = ControllerMode.Client;
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
                    _client.ServerDisconnected -= Client_ServerDisconnected;
                    _client.Stop();
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
    }
}
