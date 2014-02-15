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
        private volatile bool _streaming;

        protected NetworkChatParticipant()
        {
            _receiving = false;
            _streaming = false;
        }

        protected UdpClient InitUpdClient(int port, int timeout = 3000)
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

            UdpClient client = InitUpdClient(Network.Properties.Settings.Default.AUDIO_RECEIVE_PORT);
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

        protected void DataReceived(IPAddress addr, byte[] data)
        {
            AudioHelper.AddSamples(data);
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
            NamedPipeClientStream pipe = new NamedPipeClientStream(".", "mic", PipeDirection.In);
            pipe.Connect();

            // read from mic and send audio data to server
            // IPEndPoint serverEndPoint = new IPEndPoint(dst, Network.Properties.Settings.Default.AUDIO_TRANSMIT_PORT);
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Broadcast, Network.Properties.Settings.Default.AUDIO_RECEIVE_PORT);
            UdpClient udpClient = InitUpdClient(Network.Properties.Settings.Default.AUDIO_TRANSMIT_PORT);
            udpClient.EnableBroadcast = true;

            _streaming = true;
            while (_streaming)
            {
                pipe.Read(buffer, 0, buffer.Length);
                udpClient.Send(buffer, buffer.Length, serverEndPoint);
            }

            // free resources
            AudioHelper.StopCapture();
            pipe.Close();
            udpClient.Close();
        }

        /// <summary>
        /// Start listening for audio stream from mic and passing it to the server.
        /// </summary>
        protected void StartStreaming()
        {
            new Thread(() => { StartStreamingLoop(); }).Start();
        }

        /// <summary>
        /// Stop listening for audio stream from mic and passing it to the server.
        /// </summary>
        protected void StopStreaming()
        {
            _streaming = false;
        }

        public void Start()
        {
            StartReceiving();
            StartStreaming();
            AudioHelper.StartPlaying(new UncompressedPcmChatCodec());
        }

        public void Stop()
        {
            AudioHelper.StopPlaying();
            StopStreaming();
            StopReceiving();
        }
    }
}
