using System;
using System.Net;


namespace Network
{
    [Serializable]
    public class ServerSummary
    {
        public IPAddress Addr { get; set; }
        public string Callsign { get; set; }
        public int ClientCount { get; set; }

        public ServerSummary(Server server)
        {
            Addr = server.Addr;
            Callsign = server.Callsign;
            ClientCount = server.Clients.Count;
        }
    }
}
