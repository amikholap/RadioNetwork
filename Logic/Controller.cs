﻿using log4net;
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
            if (Mode == ControllerMode.Server)
            {
                // stop server process
                _server.Stop();
                _server = null;
            }
            _client = new Client(callsign, fr, ft);
            Mode = ControllerMode.Client;

            _client.DetectServer();
        }

        /// <summary>
        /// Run application in server mode.
        /// </summary>
        public static void StartServer()
        {
            switch (Mode)
            {
                case ControllerMode.None:
                    _server = new Server();
                    _server.Start();
                    Mode = ControllerMode.Server;
                    break;
                case ControllerMode.Client:
                    break;
                case ControllerMode.Server:
                    break;
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
                    break;
                case ControllerMode.Server:
                    _server.Stop();
                    break;
            }
        }

        public static void Start()
        {
            _server.Start();

            System.Threading.Thread.Sleep(5000);
            System.Net.Sockets.UdpClient writer = new System.Net.Sockets.UdpClient();
            writer.EnableBroadcast = true;
            writer.Connect(new System.Net.IPEndPoint(System.Net.IPAddress.Broadcast, 41853));
            byte[] dgram = Encoding.ASCII.GetBytes("client");
            writer.Send(dgram, dgram.Length);
            System.Threading.Thread.Sleep(1000);
            writer.Send(dgram, dgram.Length);
            writer.Close();
            _server.WriteTCP();


            //var h = new Audio.AudioHelper();
            //h.CaptureFromMic();
            // h.PlayCaptured();
        }
    }
}
