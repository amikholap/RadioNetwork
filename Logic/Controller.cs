using log4net;
using Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


[assembly: log4net.Config.XmlConfigurator(Watch = true)]


namespace Logic
{
    public static class Controller
    {
        private static Client _client;
        private static Server _server;

        private static ILog logger = LogManager.GetLogger("RadioNetwork");

        public static ControllerMode Mode { get; set; }

        static Controller()
        {
            _client = null;
            _server = null;
            Mode = ControllerMode.None;
        }

        /// <summary>
        /// Run application in client mode.
        /// </summary>
        /// <param name="callsign"></param>
        /// <param name="fr"></param>
        /// <param name="ft"></param>
        public static void StartClient(string callsign, int fr, int ft)
        {
            if (Mode != ControllerMode.Client)
            {
                // stop any existing non client processes
                Stop();
            }

            // create Client instance
            _client = new Client(callsign, fr, ft);

            // find a server and connect to it
            try
            {
                _client.Connect();
                Mode = ControllerMode.Client;
            }
            catch (Exception e)
            {
                logger.Error("Couldn't start client: " + e.Message);
                return;
            }

            // start streaming
            _client.StartStreaming();
        }

        /// <summary>
        /// Run application in server mode.
        /// </summary>
        public static void StartServer()
        {
            if (Mode != ControllerMode.Server)
            {
                // stop any existing non server processes
                Stop();
            }

            // create Server instance
            _server = new Server();

            // try to start server
            try
            {
                _server.Start();
                Mode = ControllerMode.Server;
            }
            catch (Exception e)
            {
                logger.Error("Couldn't start server: " + e.Message);
                return;
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
                    _client.Disconnect();
                    break;
                case ControllerMode.Server:
                    _server.Stop();
                    break;
            }
        }
    }
}
