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
        private static WaveInEvent _waveIn;
        private static WaveOut _waveOut;
        private static WaveFileWriter _audioLog;

        static AudioHelper()
        {
            _waveIn = new WaveInEvent();
            _waveIn.BufferMilliseconds = 50;
            _waveIn.DataAvailable += (sender, e) =>
            {
                AudioIO.AddOutputData(e.Buffer, null);
            };
        }

        /// <summary>
        /// Capture audio stream from the specified input device
        /// in codec's format and add it to the output queue.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="codec"></param>
        /// <param name="inputDeviceNumber"></param>
        public static void StartCapture(INetworkChatCodec codec, int inputDeviceNumber = 0)
        {
            _waveIn.DeviceNumber = inputDeviceNumber;
            _waveIn.WaveFormat = codec.RecordFormat;
            _waveIn.StartRecording();
        }

        public static void StopCapture()
        {
            _waveIn.StopRecording();
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
            _playBuffer.BufferDuration = TimeSpan.FromMilliseconds(150);
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

        private static void AudioLogTickCallback(object sender, AudioIOEventArgs e)
        {
            if (e.Item != null)
            {
                _audioLog.Write(e.Item.Data, 0, e.Item.Data.Length);
            }
            else
            {
                // this array can be created only once
                float[] silence = new float[(int)(AudioIO.TickInterval.TotalSeconds * _audioLog.WaveFormat.SampleRate)];
                _audioLog.WriteSamples(silence, 0, silence.Length);
            }
        }

        /// <summary>
        /// Start logging audio data to a file.
        /// </summary>
        public static void StartLogging(string path, INetworkChatCodec codec)
        {
            _audioLog = new WaveFileWriter(path, codec.RecordFormat);
            AudioIO.MergedTick += AudioLogTickCallback;
        }

        /// <summary>
        /// Stop logging audio.
        /// </summary>
        public static void StopLogging()
        {
            AudioIO.MergedTick -= AudioLogTickCallback;
            _audioLog.Close();
        }

        /// <summary>
        /// Add samples to the playback queue and try to log them.
        /// </summary>
        /// <param name="samples"></param>
        public static void AddSamples(byte[] samples)
        {
            _playBuffer.AddSamples(samples, 0, samples.Length);
        }
    }
}
