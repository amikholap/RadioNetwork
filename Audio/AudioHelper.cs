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
        private static BufferedWaveProvider playBuffer;
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

        public static void AddSamples(byte[] samples)
        {
            playBuffer.AddSamples(samples, 0, samples.Length);
        }

        public static void StartPlaying(INetworkChatCodec codec)
        {
            // data provider for WaveOut
            playBuffer = new BufferedWaveProvider(codec.RecordFormat);
            // BufferDuration == lag
            playBuffer.BufferDuration = new TimeSpan(hours: 0, minutes: 0, seconds: 1);
            playBuffer.DiscardOnBufferOverflow = true;

            // output device
            _waveOut = new WaveOut();
            _waveOut.Init(playBuffer);
            _waveOut.Play();
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
