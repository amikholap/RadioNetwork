using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Audio
{
    public class AudioQueueItem
    {
        /// <summary>
        /// A chunk of compressed audio data.
        /// </summary>
        public byte[] Data { get; set; }
        /// <summary>
        /// Timestamp when this item was added to a queue.
        /// </summary>
        public DateTime Timestamp { get; set; }
        /// <summary>
        /// Arbitrary context.
        /// </summary>
        public object Context { get; set; }

        public AudioQueueItem(byte[] data, DateTime ts, object ctx)
        {
            Data = data;
            Timestamp = ts;
            Context = ctx;
        }
    }
}
