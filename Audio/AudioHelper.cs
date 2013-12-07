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

        private static readonly ILog logger = LogManager.GetLogger("RadioNetwork");

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
            waveOut.Play();

            try
            {
                while (true)
                {
                    pipe.BeginRead(buffer, 0, buffer.Length, (r) => { playBuffer.AddSamples(buffer, 0, buffer.Length); pipe.EndRead(r); }, null);
                }
            }
            catch (ThreadAbortException)
            {
            }
            catch (Exception e)
            {
                logger.Error("Unhandled exception while playing audio data from pipe.", e);
            }
            finally
            {
                waveOut.Stop();
                pipe.Close();
            }
        }
    }
}
