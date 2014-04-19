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

        /// <summary>
        /// Return a list of available server IP addresses.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPAddress> DetectServers()
        {
            Byte[] dgram = new byte[256];
            List<IPAddress> serverIPs = new List<IPAddress>();

            UdpClient udpClient = NetworkHelper.InitUdpClient(Network.Properties.Settings.Default.BROADCAST_PORT);
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
            double Delta = 0;
            bool th = false;
            DateTime dtStart;
            base._connectPing = true;
            while (base._connectPing == true)
            {
                dtStart = DateTime.Now;
                th = StartAsyncPing(_servAddr, Network.Properties.Settings.Default.PING_PORT_IN_SERVER);
                Delta = (DateTime.Now - dtStart).TotalMilliseconds;
                if (th == false)
                {
                    Stop();                    
                    OnClientEvent(new ClientEventArgs(string.Format("Соединение с сервером {0} разорвано", _servAddr)));
                }
                else
                {
                    if ((pingWaitReply - (int)Delta) > 300)
                        Thread.Sleep(pingWaitReply - (int)Delta);
                }
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
            UpdateClientInfo(this.Callsign, this.Fr, this.Ft, "DELETE", 1);
            this._servAddr = null;
            base.Stop();
            Thread.Sleep(1000);    // let worker threads finish
        }
    }
}
