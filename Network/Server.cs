using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


[assembly: log4net.Config.XmlConfigurator(Watch = true)]


namespace Network
{
    public class Server
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Namespace);

        public void Listen()
        {
            TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 59555);
            listener.Start();

            while (true)
            {
                using (TcpClient c = listener.AcceptTcpClient())
                using (NetworkStream ns = c.GetStream())
                {
                    var buf = new byte[100];
                    ns.Read(buf, 0, buf.Length);
                    Server.log.Debug(Encoding.UTF8.GetString(buf));
                }
                Thread.Sleep(100);
            }
        }

        public void Write()
        {
            using (TcpClient client = new TcpClient("127.0.0.1", 59555))
            using (NetworkStream ns = client.GetStream())
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes("message");
                ns.Write(data, 0, data.Length);
            }
        }
    }
}
