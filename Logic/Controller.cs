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
        private static Server _server;

        private static ILog logger = LogManager.GetLogger("RadioNetwork");

        static Controller()
        {
            _server = new Server();
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

        public static void Stop()
        {
            _server.Stop();
        }
    }
}
