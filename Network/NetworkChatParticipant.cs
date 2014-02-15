using Audio;
using log4net;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Network
{
    public class NetworkChatParticipant
    {
        protected static readonly ILog logger = LogManager.GetLogger("RadioNetwork");

        private volatile bool _receiving;

        private NamedPipeClientStream _micPipe;
        private UdpClient _streamClient;
        private Thread _streamingThread;

        /// <summary>
        /// Callback that is executed when any audio data is received.
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="data"></param>
        protected virtual void DataReceived(IPAddress addr, byte[] data) { }

        /// <summary>
        /// Machine's IP address.
        /// </summary>
        public IPAddress Addr { get; set; }

        protected NetworkChatParticipant()
        {
            _receiving = false;
            Addr = NetworkHelper.GetLocalIPAddress();
        }

        protected UdpClient InitUdpClient(int port, int timeout = 3000)
        {
            // create client
            UdpClient udpClient = new UdpClient(port);

            // set timeouts
            udpClient.Client.ReceiveTimeout = timeout;
            udpClient.Client.SendTimeout = timeout;

            // reuse ports
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            return udpClient;
        }

        private void StartReceivingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.MAX_BUFFER_SIZE];

            UdpClient client = InitUdpClient(Network.Properties.Settings.Default.AUDIO_RECEIVE_PORT);
            client.EnableBroadcast = true;
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.AUDIO_RECEIVE_PORT);

            _receiving = true;
            while (_receiving)
            {
                try
                {
                    buffer = client.Receive(ref clientEndPoint);
                }
                catch (SocketException)
                {
                    // timeout
                    continue;
                }
                DataReceived(clientEndPoint.Address, buffer);
            }
            client.Close();
        }

        protected void StartReceiving()
        {
            new Thread(StartReceivingLoop).Start();
        }

        protected void StopReceiving()
        {
            _receiving = false;
        }

        private void StartStreamingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.BUFFER_SIZE];

            // launch a thread that captures audio stream from mic and writes it to "mic" named pipe
            new Thread(() => AudioHelper.StartCapture(new UncompressedPcmChatCodec())).Start();

            // open pipe to read audio data from microphone
            _micPipe = new NamedPipeClientStream(".", "mic", PipeDirection.In);
            _micPipe.Connect();

            // read from mic and send audio data to server
            // IPEndPoint serverEndPoint = new IPEndPoint(dst, Network.Properties.Settings.Default.AUDIO_TRANSMIT_PORT);
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Broadcast, Network.Properties.Settings.Default.AUDIO_RECEIVE_PORT);
            _streamClient = InitUdpClient(Network.Properties.Settings.Default.AUDIO_TRANSMIT_PORT);
            _streamClient.EnableBroadcast = true;

            while (true)
            {
                _micPipe.Read(buffer, 0, buffer.Length);
                _streamClient.Send(buffer, buffer.Length, serverEndPoint);
            }
        }

        /// <summary>
        /// Start listening for audio stream from mic and passing it to the server.
        /// </summary>
        public void StartStreaming()
        {
            _streamingThread = new Thread(StartStreamingLoop);
            _streamingThread.Start();
        }

        /// <summary>
        /// Stop listening for audio stream from mic and passing it to the server.
        /// </summary>
        public void StopStreaming()
        {
            AudioHelper.StopCapture();
            _streamingThread.Abort();
            _micPipe.Close();
            _streamClient.Close();
        }

        public void Start()
        {
            AudioHelper.StartPlaying(new UncompressedPcmChatCodec());
            StartReceiving();
        }

        public void Stop()
        {
            StopReceiving();
            AudioHelper.StopPlaying();
        }
    }
}
