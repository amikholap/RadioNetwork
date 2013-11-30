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
    public class AudioHelper
    {
        private NamedPipeServerStream _pipe;

        private static readonly ILog log = LogManager.GetLogger("RadioNetwork");

        public AudioHelper()
        {
            IAsyncResult r = null;
            _pipe = new NamedPipeServerStream("mic", PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            r = _pipe.BeginWaitForConnection((o) => { _pipe.EndWaitForConnection(r); }, null);
        }

        private void waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            _pipe.Write(e.Buffer, 0, e.BytesRecorded);
        }

        /// <summary>
        /// Capture audio stream from the specified input device in codec's format
        /// and write it to the "mic" named pipe.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="codec"></param>
        /// <param name="inputDeviceNumber"></param>
        public void StartCapture(INetworkChatCodec codec, int inputDeviceNumber = 0)
        {
            WaveInEvent waveIn = new WaveInEvent();
            waveIn.BufferMilliseconds = 50;
            waveIn.DeviceNumber = inputDeviceNumber;
            waveIn.WaveFormat = codec.RecordFormat;
            waveIn.DataAvailable += waveIn_DataAvailable;
            waveIn.StartRecording();
        }

        public static void StartPlaying(object codec)
        {
            INetworkChatCodec cdc = (INetworkChatCodec)codec;
            byte[] buffer = new byte[100];
            NamedPipeClientStream pipe = new NamedPipeClientStream(".", "audio", PipeDirection.In);
            BufferedWaveProvider playBuffer = new BufferedWaveProvider(cdc.RecordFormat);
            WaveOut waveOut = new WaveOut();

            waveOut.Init(playBuffer);
            waveOut.Play();

            while (true)
            {
                pipe.Read(buffer, 0, buffer.Length);
                playBuffer.AddSamples(buffer, 0, buffer.Length);
            }
        }
    }
}
