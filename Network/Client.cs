using Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;
using System.IO.Pipes;

namespace Network
{
    public class Client : NetworkChatParticipant
    {
        private IPAddress _serverIP;

        /// <summary>
        /// Client's callsign.
        /// </summary>
        public string Callsign { get; set; }
        /// <summary>
        /// Receive frequency.
        /// </summary>
        public int Fr { get; set; }
        /// <summary>
        /// Transmit frequency.
        /// </summary>
        public int Ft { get; set; }


        public Client(string callsign, int fr, int ft)
            : base()
        {
            Callsign = callsign;
            Fr = fr;
            Ft = ft;
        }

        public Client(IPAddress addr, string callsign, int fr, int ft)
            : this(callsign, fr, ft)
        {
            Addr = addr;
        }

        private IEnumerable<IPAddress> DetectServers()
        {
            Byte[] dgram = new byte[256];
            List<IPAddress> serverIPs = new List<IPAddress>();

            UdpClient udpClient = InitUpdClient(Network.Properties.Settings.Default.BROADCAST_PORT);
            udpClient.EnableBroadcast = true;
            // determine port && BroadcastAddr
            IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, Network.Properties.Settings.Default.BROADCAST_PORT);
            IPEndPoint anyEndPoint = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.BROADCAST_PORT);

            // send BroadCast message "client" in byte[]
            string request = "client";
            dgram = Encoding.UTF8.GetBytes(request);
            // Blocks until a message returns on this socket from a remote host.
            udpClient.Send(dgram, dgram.Length, broadcastEndPoint);

            // listen for server's reponse for 5 seconds
            DateTime t = DateTime.Now;
            try
            {
                // wait for server's response for 5 seconds
                while ((DateTime.Now - t) < TimeSpan.FromSeconds(5))
                {
                    dgram = udpClient.Receive(ref anyEndPoint);
                    string response = Encoding.UTF8.GetString(dgram);
                    // data is in format {client,server}
                    if (response == "server")
                    {
                        serverIPs.Add(anyEndPoint.Address);
                        logger.Debug(String.Format("Found server: {0}", anyEndPoint.Address));
                    }
                }
            }
            catch (SocketException)
            {
                // timeout
            }
            catch (Exception e)
            {
                logger.Error("Unhandled exception while detecting servers.", e);
            }
            finally
            {
                udpClient.Close();
            }

            return serverIPs;
        }

        /// <summary>
        /// Send to server updated info about this client to server.
        /// </summary>
        public void UpdateClientInfo()
        {
            Byte[] dgram = new Byte[256];
            IPEndPoint ipEndPoint = new IPEndPoint(_serverIP, Network.Properties.Settings.Default.TCP_PORT);
            TcpClient tcpClient = new TcpClient();

            try
            {
                tcpClient.Connect(ipEndPoint);

                string message = String.Format("UPDATE\n{0}\n{1},{2}", Callsign, Fr, Ft);
                dgram = System.Text.Encoding.UTF8.GetBytes(message);
                using (NetworkStream ns = tcpClient.GetStream())
                {
                    ns.Write(dgram, 0, dgram.Length);
                }
            }
            catch (Exception e)
            {
                logger.Error("Unhandled exception while sending client's info.", e);
                return;
            }
            finally
            {
                tcpClient.Close();
            }
        }

        /// <summary>
        /// Connect to a server and start streaming audio.
        /// </summary>
        public void Start()
        {
            IEnumerable<IPAddress> serverIPs = DetectServers();
            if (serverIPs.Count() > 0)
            {
                _serverIP = serverIPs.First();
                UpdateClientInfo();
                base.Start();
            }
        }

        /// <summary>
        /// Stop client.
        /// </summary>
        public void Stop()
        {
            base.Stop();
        }
    }
}
