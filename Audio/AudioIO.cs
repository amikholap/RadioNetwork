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
        /// Event that fires either when input data are available
        /// with AudioQueueItem instance as an argument
        /// or every `n` milliseconds with null argument if there are no data.
        /// </summary>
        public static event EventHandler<AudioIOEventArgs> InputTick;
        /// <summary>
        /// Event that fires either when output data are available
        /// with AudioQueueItem instance as an argument
        /// or every `n` milliseconds with null argument if there are no data.
        /// </summary>
        public static event EventHandler<AudioIOEventArgs> OutputTick;

        static AudioIO()
        {
            _inputQueue = new ConcurrentQueue<AudioQueueItem>();
            _outputQueue = new ConcurrentQueue<AudioQueueItem>();
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
            bool haveData;
            AudioQueueItem item;

            _isTicking = true;
            while (_isTicking)
            {
                haveData = false;

                if (_inputQueue.TryDequeue(out item))
                {
                    OnInputTick(new AudioIOEventArgs(item));
                    haveData = true;
                }
                else
                {
                    OnInputTick(new AudioIOEventArgs(null));
                }

                if (_outputQueue.TryDequeue(out item))
                {
                    OnOutputTick(new AudioIOEventArgs(item));
                    haveData = true;
                }
                else
                {
                    OnOutputTick(new AudioIOEventArgs(null));
                }

                if (!haveData)
                {
                    Thread.Sleep(50);
                }
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
