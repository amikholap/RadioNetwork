using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    public class ServerDisconnectedEventArgs : EventArgs
    {
        public readonly string Message;

        public ServerDisconnectedEventArgs(string msg)
        {
            Message = msg;
        }
    }
}
