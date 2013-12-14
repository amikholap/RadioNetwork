using log4net;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Audio
{
    public static class AudioHelper
    {
        private static readonly ILog logger = LogManager.GetLogger("RadioNetwork");
        private static WaveInEvent _waveIn;
        private static WaveOut _waveOut;

        /// <summary>
        /// Capture audio stream from the specified input device in codec's format
        /// and write it to the "mic" named pipe.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="codec"></param>
        /// <param name="inputDeviceNumber"></param>
        public static void StartCapture(INetworkChatCodec codec, int inputDeviceNumber = 0)
        {
            // pipe for audio data from mic
            NamedPipeServerStream micPipe = new NamedPipeServerStream("mic", PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            micPipe.BeginWaitForConnection((r) => { micPipe.EndWaitForConnection(r); }, null);

            _waveIn = new WaveInEvent();
            _waveIn.BufferMilliseconds = 50;
            _waveIn.DeviceNumber = inputDeviceNumber;
            _waveIn.WaveFormat = codec.RecordFormat;
            _waveIn.DataAvailable += (sender, e) => { micPipe.Write(e.Buffer, 0, e.BytesRecorded); };
            _waveIn.RecordingStopped += (sender, e) => { micPipe.Close(); };
            _waveIn.StartRecording();
        }

        public static void StopCapture()
        {
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
            }
        }

        /// <summary>
        /// Callback that is called after successfull read from pipe.
        /// </summary>
        /// <param name="r"></param>
        private static void AddSamples(IAsyncResult r)
        {
            var state = (Tuple<NamedPipeClientStream, BufferedWaveProvider, byte[]>)r.AsyncState;
            var pipe = state.Item1;
            var provider = state.Item2;
            var buffer = state.Item3;

            provider.AddSamples(buffer, 0, buffer.Length);
            pipe.EndRead(r);
            if (pipe.IsConnected)
            {
                pipe.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(AddSamples), state);
            }
        }

        /// <summary>
        /// Read audio data from named pipe 'net' and play it.
        /// Data format is determined by codec.
        /// </summary>
        /// <param name="codec"></param>
        public static void StartPlaying(INetworkChatCodec codec)
        {
            byte[] buffer = new byte[100];
            BufferedWaveProvider playBuffer;

            // pipe for audio data from network
            NamedPipeClientStream netPipe = new NamedPipeClientStream(".", "net", PipeDirection.In, PipeOptions.Asynchronous);
            netPipe.Connect();

            // data provider for WaveOut
            playBuffer = new BufferedWaveProvider(codec.RecordFormat);
            // BufferDuration == lag
            playBuffer.BufferDuration = new TimeSpan(hours: 0, minutes: 0, seconds: 1);
            playBuffer.DiscardOnBufferOverflow = true;

            // output device
            _waveOut = new WaveOut();
            _waveOut.Init(playBuffer);
            _waveOut.PlaybackStopped += (sender, e) => { netPipe.Close(); };
            _waveOut.Play();

            var state = new Tuple<NamedPipeClientStream, BufferedWaveProvider, byte[]>(netPipe, playBuffer, buffer);
            netPipe.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(AddSamples), state);
        }

        /// <summary>
        /// Stop playing audio.
        /// </summary>
        public static void StopPlaying()
        {
            if (_waveOut != null)
            {
                _waveOut.Stop();
            }
        }
    }
}
