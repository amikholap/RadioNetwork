using log4net;
using Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logic
{
    public static class Main
    {
        private static Server _server = new Server();

        public static void Run()
        {
            _server.Start();
            
            ILog log = LogManager.GetLogger("RadioNetwork");

            System.Threading.Thread.Sleep(1000);
            System.Net.Sockets.UdpClient writer = new System.Net.Sockets.UdpClient();
            writer.EnableBroadcast = true;
            writer.Connect(new System.Net.IPEndPoint(System.Net.IPAddress.Broadcast, 41853));
            byte[] dgram = Encoding.ASCII.GetBytes("client");
            writer.Send(dgram, dgram.Length);
            _server.WriteTCP();
            

            // var h = new Audio.AudioHelper();
            // h.CaptureFromMic();
            // h.PlayCaptured();
        }

        public static void Stop()
        {
            _server.Stop();
        }
    }
}
