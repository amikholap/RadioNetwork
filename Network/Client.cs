﻿using Audio;
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
using System.Runtime.Serialization.Formatters.Binary;

namespace Network
{
    public class Client : NetworkChatParticipant
    {
        private IPAddress _servAddr;
        private UdpClient _streamClient;
        private int pingWaitReply = 9000;

        /// <summary>
        /// Receive frequency.
        /// </summary>
        public UInt32 Fr { get; set; }
        /// <summary>
        /// Transmit frequency.
        /// </summary>
        public UInt32 Ft { get; set; }
        /// <summary>
        /// IP multicast group where the client will send audio data.
        /// </summary>
        /// <summary>
        /// Server IP
        /// </summary>
        public IPAddress ServAddr { get { return _servAddr; } }
        public IPAddress TransmitMulticastGroupAddr { get; set; }
        /// <summary>
        /// IP multicast group where the client will listen for audio data.
        /// </summary>
        public IPAddress ReceiveMulticastGroupAddr { get; set; }


        public event EventHandler<EventArgs> ServerQuit;
        public event EventHandler<ClientEventArgs> ServerDisconnected;

        public virtual void OnClientEvent(ClientEventArgs e)
        {
            if (ServerDisconnected != null)
                ServerDisconnected(this, e);
        }

        public Client(string callsign, UInt32 fr, UInt32 ft)
            : base(callsign)
        {
            Fr = fr;
            Ft = ft;
            UpdateMulticastAddrs();
        }

        public Client(IPAddress addr, string callsign, UInt32 fr, UInt32 ft)
            : this(callsign, fr, ft)
        {
            Addr = addr;
        }

        protected virtual void OnServerQuit(EventArgs e)
        {
            if (ServerQuit != null)
            {
                ServerQuit(this, e);
            }
        }

        protected void UpdateMulticastAddrs()
        {
            TransmitMulticastGroupAddr = NetworkHelper.FreqToMcastGroup(Ft);
            ReceiveMulticastGroupAddr = NetworkHelper.FreqToMcastGroup(Fr);
        }

        public static IEnumerable<ServerSummary> DetectServers()
        {
            int waitTime = 1000;
            Byte[] dgram = new byte[0];
            List<IPAddress> serverAddresses = new List<IPAddress>();
            List<ServerSummary> servers = new List<ServerSummary>();

            UdpClient sendClient = NetworkHelper.InitUdpClient();
            sendClient.EnableBroadcast = true;
            IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, Properties.Settings.Default.BROADCAST_PORT);
            try
            {
                sendClient.Send(dgram, dgram.Length, broadcastEndPoint);
            }
            catch (SocketException)
            {
                // timeout
            }
            finally
            {
                sendClient.Close();
            }

            UdpClient receiveClient = NetworkHelper.InitUdpClient(Properties.Settings.Default.BROADCAST_PORT);
            receiveClient.Client.ReceiveTimeout = waitTime;
            DateTime startTime = DateTime.Now;
            IPEndPoint serverEP;

            while (DateTime.Now - startTime < TimeSpan.FromMilliseconds(waitTime))
            {
                serverEP = null;
                try
                {
                    receiveClient.Receive(ref serverEP);
                }
                catch (SocketException)
                {
                    // timeout
                }
                if (serverEP != null)
                {
                    serverAddresses.Add(serverEP.Address);
                    logger.Debug(String.Format("Server responded: {0}", serverAddresses.Last()));
                }
            }
            receiveClient.Close();

            foreach (var servAddr in serverAddresses)
            {
                ServerSummary s = GetServerSummary(servAddr);
                if (s != null)
                {
                    servers.Add(s);
                    break;
                }
            }

            return servers;
        }

        /// <summary>
        /// Return a list of available servers.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<ServerSummary> DetectServers1()
        {
            int waitTime = 1000;
            Byte[] dgram = new byte[0];
            List<IPAddress> serverAddresses = new List<IPAddress>();
            List<ServerSummary> servers = new List<ServerSummary>();

            // place code that listen for server responses and fills serverAddresses in a callback function
            AsyncCallback sendCallback = sendAR =>
                {
                    UdpClient sndClient = (UdpClient)sendAR.AsyncState;
                    UdpClient receiveClient = NetworkHelper.InitUdpClient(Properties.Settings.Default.BROADCAST_PORT);
                    DateTime startTime = DateTime.Now;

                    // save server IP address during receive callback
                    AsyncCallback receiveCallback = receiveAR =>
                        {
                            IPEndPoint ep = null;
                            UdpClient recvClient = (UdpClient)receiveAR.AsyncState;

                            recvClient.EndReceive(receiveAR, ref ep);
                            serverAddresses.Add(ep.Address);
                            logger.Debug(String.Format("Server responded: {0}", serverAddresses.Last()));

                            if (DateTime.Now - startTime < TimeSpan.FromMilliseconds(waitTime))
                            {
                                // `waitTime` isn't over - wait for more responses
                                // recvClient.BeginReceive(receiveCallback, recvClient);
                            }
                            else
                            {
                                recvClient.Close();
                            }
                        };

                    receiveClient.BeginReceive(receiveCallback, receiveClient);

                    sndClient.EndSend(sendAR);
                    sndClient.Close();
                };

            // ask every device in the local network
            UdpClient sendClient = NetworkHelper.InitUdpClient();
            sendClient.EnableBroadcast = true;
            IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, Properties.Settings.Default.BROADCAST_PORT);
            Task.Run(async () =>
                {
                    var result = await sendClient.SendAsync(dgram, dgram.Length, broadcastEndPoint);
                });

            foreach (var servAddr in serverAddresses)
            {
                ServerSummary s = GetServerSummary(servAddr);
                if (s != null)
                {
                    servers.Add(s);
                    break;
                }
            }

            return servers;
        }

        /// <summary>
        /// Fetch details for a server with the specified IP address.
        /// Return a deserialized Server object or null if something went wrong.
        /// </summary>
        /// <param name="addr"></param>
        /// <returns></returns>
        private static ServerSummary GetServerSummary(IPAddress addr)
        {
            IPEndPoint ep = new IPEndPoint(addr, Properties.Settings.Default.SERVER_DETAILS_PORT);
            TcpClient c = new TcpClient();
            c.ReceiveTimeout = 2000;

            ServerSummary server = null;

            try
            {
                c.Connect(ep);
                using (NetworkStream ns = c.GetStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    server = (ServerSummary)bf.Deserialize(ns);
                }
            }
            catch (Exception e)
            {
                logger.Error("Unhandled exception while fetching server summary.", e);
            }
            finally
            {
                c.Close();
            }

            return server;
        }

        /// <summary>
        /// Send to server updated info about this client to server.
        /// </summary>
        public string UpdateClientInfo(string NewCallsign, uint NewFr, uint NewFt, string NewMess = "UPDATE", int TryCount = 3)
        {
            Byte[] dgram = new Byte[256];
            IPEndPoint ipEndPoint = new IPEndPoint(_servAddr, Network.Properties.Settings.Default.TCP_PORT);
            TcpClient tcpClient = new TcpClient();
            String responseData = String.Empty;
            try
            {
                tcpClient.Connect(ipEndPoint);
                string message = String.Format("{0}\n{1}\n{2},{3}", NewMess, NewCallsign, NewFr, NewFt);
                dgram = System.Text.Encoding.UTF8.GetBytes(message);
                logger.Debug(String.Format("send to server {0}: '{1}'", this._servAddr, message));

                Byte[] data = new Byte[64];
                Int32 bytes = 0, count = 0;
                using (NetworkStream ns = tcpClient.GetStream())
                {
                    ns.Write(dgram, 0, dgram.Length);
                    while (count < TryCount && bytes == 0)
                    {
                        Thread.Sleep((pingWaitReply / 9));
                        bytes = ns.Read(data, 0, data.Length);
                        count++;
                    }
                    responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
                }
                logger.Debug(String.Format("server {0} reply: '{1}'", this._servAddr, responseData));
            }
            catch (Exception e)
            {
                logger.Error("Unhandled exception while sending client's info.", e);
            }
            finally
            {
                tcpClient.Close();
            }
            return responseData;
        }

        protected override void StartSendPingLoop()
        {
            base._connectPing = true;
            while (base._connectPing == true)
            {
                if (StartAsyncPing(_servAddr, Network.Properties.Settings.Default.PING_PORT_IN_SERVER) == false)
                {
                    IPAddress serverAddress = _servAddr;
                    Stop();
                    OnClientEvent(new ClientEventArgs(string.Format("Соединение с сервером {0} разорвано", serverAddress)));
                }
                Thread.Sleep(pingWaitReply);
            }
        }

        /// <summary>
        /// Send audio from a microphone to the server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void AudioIO_OutputDataAvailable(object sender, AudioIOEventArgs e)
        {
            if (e.Item != null)
            {
                try
                {
                    _streamClient.Send(e.Item.Data, e.Item.Data.Length, new IPEndPoint(_servAddr, Network.Properties.Settings.Default.SERVER_AUDIO_PORT));
                }
                catch (ObjectDisposedException)
                {
                    // NAudio may send some more data after stopped
                    // but this UDP client may be already destroyed.
                }
            }
        }

        public override void StartStreaming()
        {
            _streamClient = NetworkHelper.InitUdpClient(Network.Properties.Settings.Default.SERVER_AUDIO_PORT);
            if (Ft == Fr)
            {
                // prevent echo
                Mute();
            }
            base.StartStreaming();
        }
        public override void StopStreaming()
        {
            base.StopStreaming();
            UnMute();
            _streamClient.Close();
        }

        /// <summary>
        /// Start receiving audio from a multicast address derived from Fr and send it to player buffer.
        /// </summary>
        protected override void StartReceivingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.MAX_BUFFER_SIZE];

            UdpClient client = NetworkHelper.InitUdpClient();
            client.JoinMulticastGroup(ReceiveMulticastGroupAddr);

            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.MULTICAST_PORT);
            client.Client.Bind(localEP);

            _receiving = true;
            while (_receiving)
            {
                try
                {
                    buffer = client.Receive(ref localEP);
                }
                catch (SocketException)
                {
                    // timeout
                    continue;
                }
                if (!_muted)
                {
                    AudioIO.AddInputData(buffer, this);
                }
            }
            client.Close();
        }

        /// <summary>
        /// Connect to a server and start streaming audio.
        /// </summary>
        public string Start(IPAddress serverAddr)
        {
            _servAddr = serverAddr;
            string reply;
            if ((reply = UpdateClientInfo(Callsign, Fr, Ft)) == "free")
            {
                base.Start();
            }
            return reply;
        }

        /// <summary>
        /// Stop client.
        /// </summary>
        public override void Stop()
        {
            // tell server that client forcing disconnect
            // server reply does not matter           
            base.Stop();
            _servAddr = null;
            Thread.Sleep(1000);    // let worker threads finish
        }
    }
}
