using Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    public static class SpeechRecognizer
    {
        private static byte[] _buffer;

        public static event EventHandler<EventArgs> SpeechRecognized;

        static SpeechRecognizer()
        {
            AudioIO.MergedTick += AudioIO_MergedTick;
        }

        private static void OnSpeechRecognized(EventArgs e)
        {
            if (SpeechRecognized != null)
            {
                SpeechRecognized(null, e);
            }
        }

        public static void AudioIO_MergedTick(object sender, AudioIOEventArgs e)
        {
            if (e.Item == null)
            {
                return;
            }

            Array.Resize(ref _buffer, _buffer.Length + e.Item.Data.Length);
            e.Item.Data.CopyTo(_buffer, _buffer.Length - e.Item.Data.Length);
        }

        public static void Server_TalkerChanged(object sender, TalkerChangedEventArgs e)
        {
            // send _buffer to Google
            OnSpeechRecognized(EventArgs.Empty);
            _buffer = new byte[0];
        }
    }
}