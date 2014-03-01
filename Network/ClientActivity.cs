using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    /// <summary>
    /// Log of client actions.
    /// </summary>
    class ClientActivity
    {
        public Client Client { get; set; }
        public DateTime last_talked { get; set; }
    }
}
