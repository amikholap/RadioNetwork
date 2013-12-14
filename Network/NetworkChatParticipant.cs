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

        private volatile bool _playing;
        private volatile bool _streaming;

        protected NetworkChatParticipant()
        {
            _playing = false;
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

        public void StartPlayingLoop()
        {
            byte[] buffer = new byte[Network.Properties.Settings.Default.MAX_BUFFER_SIZE];

            // open pipe to read audio data from network
            NamedPipeServerStream netPipe = new NamedPipeServerStream("net", PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            netPipe.BeginWaitForConnection((r) => { netPipe.EndWaitForConnection(r); }, null);

            UdpClient client = InitUpdClient(Network.Properties.Settings.Default.AUDIO_RECEIVE_PORT);
            client.EnableBroadcast = true;
            // TODO: replace IPAddress.Any with something more specific
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, Network.Properties.Settings.Default.AUDIO_RECEIVE_PORT);

            _playing = true;
            while (_playing)
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
                if (netPipe.IsConnected)
                {
                    try
                    {
                        netPipe.Write(buffer, 0, buffer.Length);
                    }
                    catch (System.IO.IOException)
                    {
                        // sometimes netPipe becomes not connected
                        logger.Info("Tried to write to a not connected 'net' pipe");
                    }
                }
            }

            // free resources
            netPipe.Close();
            client.Close();
        }

        public void StartPlaying()
        {
            new Thread(StartPlayingLoop).Start();
            AudioHelper.StartPlaying(new UncompressedPcmChatCodec());
        }

        public void StopPlaying()
        {
            AudioHelper.StopPlaying();
            _playing = false;
        }

        private void StartStreamingLoop(/*IPAddress dst*/)
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
        protected void StartStreaming(/*IPAddress dst*/)
        {
            new Thread(() => { StartStreamingLoop(/*dst*/); }).Start();
        }

        /// <summary>
        /// Stop listening for audio stream from mic and passing it to the server.
        /// </summary>
        protected void StopStreaming()
        {
            _streaming = false;
        }
    }
}
