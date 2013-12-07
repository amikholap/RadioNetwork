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
        private static NamedPipeServerStream _pipe;
        private static WaveInEvent _waveIn;
        private static WaveOut _waveOut;

        static AudioHelper()
        {
            _pipe = new NamedPipeServerStream("mic", PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            _pipe.BeginWaitForConnection((r) => { _pipe.EndWaitForConnection(r); }, null);
            _waveIn = new WaveInEvent();
            _waveOut = new WaveOut();
        }

        /// <summary>
        /// Capture audio stream from the specified input device in codec's format
        /// and write it to the "mic" named pipe.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="codec"></param>
        /// <param name="inputDeviceNumber"></param>
        public static void StartCapture(INetworkChatCodec codec, int inputDeviceNumber = 0)
        {
            _waveIn.BufferMilliseconds = 50;
            _waveIn.DeviceNumber = inputDeviceNumber;
            _waveIn.WaveFormat = codec.RecordFormat;
            _waveIn.DataAvailable += (sender, e) => { _pipe.Write(e.Buffer, 0, e.BytesRecorded); };
            _waveIn.StartRecording();
        }

        public static void StopCapture()
        {
            _waveIn.StopRecording();
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
        /// Read audio data from named pipe 'audio' and play it.
        /// Data format is determined by codec.
        /// </summary>
        /// <param name="codec"></param>
        public static void StartPlaying(object codec)
        {
            byte[] buffer;
            NamedPipeClientStream pipe;
            BufferedWaveProvider playBuffer;
            WaveOut waveOut;

            buffer = new byte[100];

            // pipe to read from 
            pipe = new NamedPipeClientStream(".", "audio", PipeDirection.In, PipeOptions.Asynchronous);
            pipe.Connect();

            // data provider for WaveOut
            playBuffer = new BufferedWaveProvider(((INetworkChatCodec)codec).RecordFormat);
            playBuffer.DiscardOnBufferOverflow = true;

            // output device
            waveOut = new WaveOut();
            waveOut.Init(playBuffer);
            waveOut.PlaybackStopped += (sender, e) => { pipe.Close(); };
            waveOut.Play();

            var state = new Tuple<NamedPipeClientStream, BufferedWaveProvider, byte[]>(pipe, playBuffer, buffer);
            pipe.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(AddSamples), state);
        }

        /// <summary>
        /// Stop playing audio.
        /// </summary>
        public static void StopPlaying()
        {
            _waveOut.Stop();
        }
    }
}
