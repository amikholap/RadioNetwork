using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Audio
{
    public class AudioIOEventArgs : EventArgs
    {
        public readonly AudioQueueItem Item;

        public AudioIOEventArgs(AudioQueueItem item)
        {
            Item = item;
        }
    }
}
