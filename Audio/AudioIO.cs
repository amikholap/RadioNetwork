using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Audio
{
    /// <summary>
    /// Static class than controls audio data flow.
    /// </summary>
    public static class AudioIO
    {
        private static volatile bool _isTicking;
        private static Thread _tickThread;

        private static ConcurrentQueue<AudioQueueItem> _inputQueue;
        private static ConcurrentQueue<AudioQueueItem> _outputQueue;

        /// <summary>
        /// Interval between tick events.
        /// </summary>
        public static TimeSpan TickInterval { get; private set; }

        /// <summary>
        /// Event that fires in `TickInterval` intervals
        /// with AudioQueueItem instance argument if input data available
        /// or with null argument if there are no input data.
        /// </summary>
        public static event EventHandler<AudioIOEventArgs> InputTick;
        /// <summary>
        /// Event that fires in `TickInterval` intervals
        /// with AudioQueueItem instance argument if output data available
        /// or with null argument if there are no output data.
        /// </summary>
        public static event EventHandler<AudioIOEventArgs> OutputTick;
        /// <summary>
        /// Event that fires in `TickInterval` intervals
        /// with AudioQueueItem instance argument containing merged input and output data
        /// or with null argument if there are no audio data at all.
        /// </summary>
        public static event EventHandler<AudioIOEventArgs> MergedTick;

        static AudioIO()
        {
            _inputQueue = new ConcurrentQueue<AudioQueueItem>();
            _outputQueue = new ConcurrentQueue<AudioQueueItem>();

            // this should probably be the same value as AudioHelper._waveIn.BufferMilliseconds
            TickInterval = TimeSpan.FromMilliseconds(50);
        }

        private static void OnInputTick(AudioIOEventArgs e)
        {
            if (InputTick != null)
            {
                InputTick(null, e);
            }
        }
        private static void OnOutputTick(AudioIOEventArgs e)
        {
            if (OutputTick != null)
            {
                OutputTick(null, e);
            }
        }
        private static void OnMergedTick(AudioIOEventArgs e)
        {
            if (OutputTick != null)
            {
                MergedTick(null, e);
            }
        }

        /// <summary>
        /// Start firing IO events.
        /// </summary>
        public static void StartTicking()
        {
            _tickThread = new Thread(StartTickingLoop);
            _tickThread.Start();
        }
        /// <summary>
        /// Stop firing IO events.
        /// </summary>
        public static void StopTicking()
        {
            _isTicking = false;
            _tickThread.Join();
        }
        private static void StartTickingLoop()
        {
            AudioQueueItem item;
            byte[] mergedData;

            _isTicking = true;
            while (_isTicking)
            {
                mergedData = null;

                if (_inputQueue.TryDequeue(out item))
                {
                    OnInputTick(new AudioIOEventArgs(item));
                    mergedData = item.Data;
                }
                else
                {
                    OnInputTick(new AudioIOEventArgs(null));
                }

                if (_outputQueue.TryDequeue(out item))
                {
                    OnOutputTick(new AudioIOEventArgs(item));
                    if (mergedData != null)
                    {
                        Array.Resize(ref mergedData, mergedData.Length + item.Data.Length);
                        item.Data.CopyTo(mergedData, mergedData.Length - item.Data.Length);
                    }
                    else
                    {
                        mergedData = item.Data;
                    }
                    mergedData = item.Data;
                }
                else
                {
                    OnOutputTick(new AudioIOEventArgs(null));
                }

                if (mergedData != null)
                {
                    OnMergedTick(new AudioIOEventArgs(new AudioQueueItem(mergedData, DateTime.Now, null)));
                }
                else
                {
                    OnMergedTick(new AudioIOEventArgs(null));
                }

                Thread.Sleep(TickInterval);
            }
        }

        public static void AddInputData(byte[] data, object context)
        {
            _inputQueue.Enqueue(new AudioQueueItem(data, DateTime.Now, context));
        }

        public static void AddOutputData(byte[] data, object context)
        {
            _outputQueue.Enqueue(new AudioQueueItem(data, DateTime.Now, context));
        }
    }
}
