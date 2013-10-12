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
        public static void Run()
        {
            Server server = new Server();

            System.Threading.Thread t = new System.Threading.Thread(server.Listen);
            t.Start();

            server.Write();
        }
    }
}
