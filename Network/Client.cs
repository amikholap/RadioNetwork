﻿using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace Network
{
    public class Client
    {
        private IPAddress serverIP;
        private TcpClient tcpClient;
        private UdpClient udpClient;
        private Thread streamingThread;

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


        private UdpClient InitUpdClient(int port, int timeout = 5000)
        {
            // create client
            udpClient = new UdpClient(port);
            // set timeouts
            udpClient.Client.ReceiveTimeout = timeout;
            udpClient.Client.SendTimeout = timeout;
            // reuse ports
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            return udpClient;
        }

        private void DetectServer()
        {
            Byte[] dgram = new byte[256];

            udpClient = InitUpdClient(Network.Properties.Settings.Default.BROADCAST_PORT);
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
                    log.Debug(String.Format("Client received: {0}", response));
                    // data is in format {client,server}
                    if (response == "server")
                    {
                        // get new server's IP address
                        serverIP = anyEndPoint.Address;
                        log.Debug(String.Format("Found server: {0}", serverIP));
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                log.Error(e.Message);
            }
            finally
            {
                udpClient.Close();
            }

            // server didn't reply while client was listening
            throw new TimeoutException("No reply from server.");
        }

        /// <summary>
        /// Send to server updated info about this client to server.
        /// </summary>
        public void UpdateClientInfo()
        {
            Byte[] dgram = new Byte[256];

            try
            {
                IPEndPoint ipEndPoint = new IPEndPoint(serverIP, Network.Properties.Settings.Default.TCP_PORT);
                tcpClient = new TcpClient();
                tcpClient.Connect(ipEndPoint);
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                return;
            }

            string message = String.Format("UPDATE\n{0}\n{1},{2}", Callsign, Fr, Ft);
            dgram = System.Text.Encoding.UTF8.GetBytes(message);
            using (NetworkStream ns = tcpClient.GetStream())
            {
                ns.Write(dgram, 0, dgram.Length);
            }

            tcpClient.Close();
        }

        private void StartStreaming(IEnumerable<byte[]> buffer)
        {
            foreach (byte[] data in buffer)
            {

            }
        }

        /// <summary>
        /// Connect to a server and start streaming audio.
        /// </summary>
        public void Connect()
        {
            DetectServer();
            UpdateClientInfo();

            // streamingThread = new Thread(new ParameterizedThreadStart(StartStreaming)
            // streamingThread.Start();
        }

        /// <summary>
        /// Disconnect from a server.
        /// </summary>
        public void Disconnect()
        {
            // streamingThread.Abort();
        }
    }
}
