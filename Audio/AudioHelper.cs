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
        private static BufferedWaveProvider _playBuffer;
        private static NamedPipeServerStream _micPipe;
        private static WaveInEvent _waveIn;
        private static WaveOut _waveOut;

        static AudioHelper()
        {
            _waveIn = new WaveInEvent();
            _waveIn.BufferMilliseconds = 50;
            _waveIn.DataAvailable += (sender, e) => { _micPipe.Write(e.Buffer, 0, e.BytesRecorded); };
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
            // pipe for audio data from mic
            _micPipe = new NamedPipeServerStream("mic", PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            _micPipe.BeginWaitForConnection((r) => _micPipe.EndWaitForConnection(r), null);

            _waveIn.DeviceNumber = inputDeviceNumber;
            _waveIn.WaveFormat = codec.RecordFormat;
            try
            {
                _waveIn.StartRecording();
            }
            catch (InvalidOperationException)
            {
                // already recording
                return;
            }
        }

        public static void StopCapture()
        {
            try
            {
                _waveIn.StopRecording();
            }
            catch (NAudio.MmException)
            {
                // recording hasn't started
            }
            _micPipe.Close();
        }

        /// <summary>
        /// Prepare to play audio.
        /// Data to play is provided using AddSamples.
        /// </summary>
        /// <param name="codec"></param>
        public static void StartPlaying(INetworkChatCodec codec)
        {
            // data provider for WaveOut
            _playBuffer = new BufferedWaveProvider(codec.RecordFormat);
            // BufferDuration == lag
            _playBuffer.BufferDuration = new TimeSpan(hours: 0, minutes: 0, seconds: 1);
            _playBuffer.DiscardOnBufferOverflow = true;

            // output device
            _waveOut = new WaveOut();
            _waveOut.Init(_playBuffer);
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

        public static void AddSamples(byte[] samples)
        {
            _playBuffer.AddSamples(samples, 0, samples.Length);
        }
    }
}
