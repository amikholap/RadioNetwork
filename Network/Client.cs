using System;
using System.Collections.Generic;
using log4net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

namespace Network
{
    public class Client
    {
        private IPAddress serverIP;
        private TcpClient tcpClient;
        private UdpClient udpClient;
        private IPEndPoint broadcastEndPoint;

        private static readonly ILog log = LogManager.GetLogger("RadioNetwork");

        /// <summary>
        /// Client's IP address.
        /// </summary>
        public IPAddress Addr { get; set; }
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
        {
            Addr = NetworkHelper.GetLocalIPAddress();
            Callsign = callsign;
            Fr = fr;
            Ft = ft;
        }

        public Client(IPAddress addr, string callsign, int fr, int ft)
            : this(callsign, fr, ft)
        {
            Addr = addr;
        }

        // by UDP
        private void udpOpen(int timeout)
        {
            // create client
            udpClient = new UdpClient(Network.Properties.Settings.Default.BROADCAST_PORT);
            // broadcast ON
            udpClient.EnableBroadcast = true;
            udpClient.Client.ReceiveTimeout = timeout;
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            // determine port && BroadcastAddr
            broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, Network.Properties.Settings.Default.BROADCAST_PORT);

        }

        private void udpSend()
        {
            Byte[] sendBytes = new Byte[256];
            // send BroadCast message "<client>" in byte[]
            string request = "client";
            sendBytes = Encoding.UTF8.GetBytes(request);
            // Blocks until a message returns on this socket from a remote host.
            udpClient.Send(sendBytes, sendBytes.Length, broadcastEndPoint);
        }

        private bool udpListen()
        {
            Byte[] receiveBytes = new Byte[256];

            try
            {
                receiveBytes = udpClient.Receive(ref broadcastEndPoint);
                string returnData = Encoding.UTF8.GetString(receiveBytes);
                log.Debug("updReceive: " + returnData.ToString());
                // data is in format {client,server}\n<ip_address>
                string type = returnData.ToString();
                if (type.Equals("server"))
                {
                    // get new client's IP address
                    serverIP = broadcastEndPoint.Address;
                    log.Debug(String.Format("Found server: {0}", serverIP.ToString()));
                    udpClient.Close();
                    return false;
                }

            }
            catch (Exception e)
            {
                log.Error(e.Message);
            }
            return true;
        }

        private void udpClose()
        {
            udpClient.Close();
        }

        public void tcpOpen()
        {
            try
            {
                // determine port && BroadcastAddr
                IPEndPoint ipEndPoint = new IPEndPoint(serverIP, Network.Properties.Settings.Default.TCP_PORT);
                // Create a TcpClient. 
                tcpClient = new TcpClient(serverIP.ToString(), Network.Properties.Settings.Default.TCP_PORT);
            }
            catch (Exception e)
            {
                log.Error(e.Message);
            }
        }

        private void tcpSend(String mess)
        {
            Byte[] sendBytes = new Byte[256];
            sendBytes = System.Text.Encoding.UTF8.GetBytes(mess);
            // Get a client stream for reading and writing. 
            NetworkStream stream = tcpClient.GetStream();
            // Send the message to the connected TcpServer. 
            stream.Write(sendBytes, 0, sendBytes.Length);
            // Close everything.
            stream.Close();
        }

        private void tcpReceive()
        {

            Byte[] receiveBytes = new Byte[256];
            // String to store the response ASCII representation.
            String responseData = String.Empty;
            // Read the first batch of the TcpServer response bytes.
            Int32 bytes;
            NetworkStream stream = tcpClient.GetStream();
            bytes = stream.Read(receiveBytes, 0, receiveBytes.Length);
            responseData = System.Text.Encoding.UTF8.GetString(receiveBytes, 0, bytes);
            log.Debug(String.Format("Client received: {0}", responseData));
            // Close everything.
            stream.Close();
        }

        private void tcpClose()
        {
            tcpClient.Close();
        }

        public void DetectServer()
        {
            udpOpen(5000);
            udpSend();
            udpListen();
            udpClose();
        }
    }
}
