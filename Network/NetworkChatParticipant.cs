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

        protected volatile bool _receiving;

        protected UdpClient _streamClient;
        private Thread _streamingThread;

        protected NamedPipeClientStream _micPipe;

        protected virtual void StartStreamingLoop() { }
        protected virtual void StartReceivingLoop() { }

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

        protected void StartReceiving()
        {
            new Thread(StartReceivingLoop).Start();
        }

        protected void StopReceiving()
        {
            _receiving = false;
        }

        /// <summary>
        /// Initialize UDP client and mic pipe and start capturing audio.
        /// </summary>
        protected void PrepareStreaming()
        {
            _streamClient = NetworkHelper.InitUdpClient(Network.Properties.Settings.Default.SERVER_AUDIO_PORT);

            // capture audio stream from mic and write it to "mic" named pipe
            AudioHelper.StartCapture(new UncompressedPcmChatCodec());

            // open pipe to read audio data from microphone
            _micPipe = new NamedPipeClientStream(".", "mic", PipeDirection.In);
            _micPipe.Connect();
        }

        /// <summary>
        /// Start listening for audio stream from mic and passing it to the server.
        /// </summary>
        public void StartStreaming()
        {
            PrepareStreaming();
            _streamingThread = new Thread(StartStreamingLoop);
            _streamingThread.Start();
        }

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
