using log4net;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Audio
{
    public class AudioHelper
    {
        private MemoryStream _stream;

        private static readonly ILog log = LogManager.GetLogger("RadioNetwork");

        public AudioHelper()
        {
            /// initialize in-memory stream with 1Mb capacity
            _stream = new MemoryStream(1024);
        }

        private void waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            /*
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = (short)((e.Buffer[i + 1] << 8) |
                                        e.Buffer[i + 0]);
                float sample32 = sample / (float)short.MaxValue;



                //Stream.Write(e.Buffer, 0, e.BytesRecorded);
            }
            */
            _stream.Write(e.Buffer, 0, e.BytesRecorded);
        }

        public IEnumerable<Byte[]> StartCapture(INetworkChatCodec codec, int inputDeviceNumber = 0)
        {
            WaveIn waveIn = new WaveIn();
            waveIn.BufferMilliseconds = 50;
            waveIn.DeviceNumber = inputDeviceNumber;
            waveIn.WaveFormat = codec.RecordFormat;
            waveIn.DataAvailable += waveIn_DataAvailable;
            waveIn.StartRecording();

            while (true)
            {
                if (_stream.Length > 0)
                {
                    yield return _stream.ToArray();
                }
                else
                {
                    Thread.Sleep(waveIn.BufferMilliseconds / 2);
                }
            }
        }
    }
}
