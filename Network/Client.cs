using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

namespace Network
{
    public class Client
    {
        IPAddress serverIP;
        TcpClient tcpClient;
        UdpClient udpClient;
        IPEndPoint broadcastEndPoint;
        Byte[] receiveBytes;
        Byte[] sendBytes;

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

        public Client()
        {
            // initial myIP
            Addr = NetworkHelper.GetLocalIPAddress();
            receiveBytes = new Byte[256];
            sendBytes = new Byte[256];
        }

        public Client(IPAddress addr, string callsign, int fr, int ft)
            : base()
        {
            Addr = addr;
        }

        // by UDP
        public void udpCreate(int timeout)
        {
            // create client
            udpClient = new UdpClient(Network.Properties.Settings.Default.BROADCAST_PORT);
            // broadcast ON
            udpClient.EnableBroadcast = true;
            udpClient.Client.ReceiveTimeout = timeout;
            // determine port && BroadcastAddr
            broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, Network.Properties.Settings.Default.BROADCAST_PORT);
        }
        public void udpSend()
        {

            // send BroadCast message "<MyIp>" in byte[]
            string name = "client\n" + Addr.ToString();
            sendBytes = Encoding.UTF8.GetBytes(name);
            // Blocks until a message returns on this socket from a remote host.
            udpClient.Send(sendBytes, sendBytes.Length, broadcastEndPoint);
            Console.WriteLine("updSend: " + name.ToString());
        }

        public bool udpListen()
        {

            try
            {
                receiveBytes = udpClient.Receive(ref broadcastEndPoint);
                string returnData = Encoding.UTF8.GetString(receiveBytes);
                Console.WriteLine("updReceive: " + returnData.ToString());
                // data is in format {client,server}\n<ip_address>
                string type = returnData.Split('\n')[0];
                string sAddr = returnData.Split('\n')[1];
                if (type.Equals("server"))
                {
                    // get new client's IP address
                    serverIP = IPAddress.Parse(sAddr);
                    Console.WriteLine("Found IP  " +
                                        serverIP.ToString());
                    udpClient.Close();
                    return true;
                }

            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException caught!!!");
                Console.WriteLine("Source : " + e.Source);
                Console.WriteLine("Message : " + e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception caught!!!");
                Console.WriteLine("Source : " + e.Source);
                Console.WriteLine("Message : " + e.Message);
            }
            return false;
        }
        public void tcpCreate()
        {
            try
            {
                // determine port && BroadcastAddr
                IPEndPoint ipEndPoint = new IPEndPoint(serverIP, Network.Properties.Settings.Default.TCP_PORT);
                // Create a TcpClient. 
                tcpClient = new TcpClient(serverIP.ToString(), Network.Properties.Settings.Default.TCP_PORT);

            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException caught!!!");
                Console.WriteLine("Source : " + e.Source);
                Console.WriteLine("Message : " + e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception caught!!!");
                Console.WriteLine("Source : " + e.Source);
                Console.WriteLine("Message : " + e.Message);
            }
        }
        public void tcpSend(String mess)
        {
            sendBytes = System.Text.Encoding.UTF8.GetBytes(mess);
            // Get a client stream for reading and writing. 
            NetworkStream stream = tcpClient.GetStream();
            // Send the message to the connected TcpServer. 
            stream.Write(sendBytes, 0, sendBytes.Length);
            Console.WriteLine("Sent: {0}", mess);
            // Close everything.
            stream.Close();
        }
        public void tcpReceive()
        {
            // String to store the response ASCII representation.
            String responseData = String.Empty;
            // Read the first batch of the TcpServer response bytes.
            Int32 bytes;
            NetworkStream stream = tcpClient.GetStream();
            bytes = stream.Read(receiveBytes, 0, receiveBytes.Length);
            responseData = System.Text.Encoding.UTF8.GetString(receiveBytes, 0, bytes);
            Console.WriteLine("Received: ", responseData);
            // Close everything.
            stream.Close();
        }

    }
}
