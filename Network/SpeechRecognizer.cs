using Audio;
using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace Network
{
    public static class SpeechRecognizer
    {
        private static byte[] _buffer;
        private static INetworkChatCodec _codec;

        public static event EventHandler<SpeechRecognizedEventArgs> SpeechRecognized;

        private static string UploadWaveToGoogle(byte[] data, INetworkChatCodec codec)
        {
            HttpWebRequest request = HttpWebRequest.CreateHttp("https://www.google.com/speech-api/v2/recognize?output=json&lang=ru-RU&key=" + Network.Properties.Settings.Default.GOOGLE_TTS_API_KEY);
            request.Method = "POST";
            request.ContentType = String.Format("audio/l16; rate={0};", codec.RecordFormat.SampleRate);
            request.ContentLength = data.Length;
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/34.0.1847.116 Safari/537.36";

            try
            {
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            }
            catch (WebException)
            {
                return "";
            }

            using (var response = request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        private static string ParseGoogleSpeechAPIResponse(string response)
        {
            string parsed;

            // fetch the most probable transcript from the response
            Match match = Regex.Match(response, "\"transcript\":\"(.*?)\"");
            if (match.Success)
            {
                parsed = match.Groups[1].Value;
            }
            else
            {
                parsed = "";
            }

            return parsed;
        }

        private static void OnSpeechRecognized(SpeechRecognizedEventArgs e)
        {
            if (SpeechRecognized != null)
            {
                SpeechRecognized(null, e);
            }
        }

        private static void AudioIO_MergedTick(object sender, AudioIOEventArgs e)
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
            Action action = () =>
                {
                    if (e.PrevTalker == null || _buffer.Length / _codec.RecordFormat.AverageBytesPerSecond < 1)
                    {
                        // do nothing if someone just started talking or
                        // the current buffer duration is under 1s
                        _buffer = new byte[0];
                        return;
                    }

                    // work with a local copy since this method is async
                    byte[] buffer = new byte[_buffer.Length];
                    Array.Copy(_buffer, buffer, _buffer.Length);
                    _buffer = new byte[0];

                    byte[] flacFileData = AudioHelper.ConstructWaveFileData(buffer, _codec);
                    string recognized = UploadWaveToGoogle(flacFileData, _codec);
                    recognized = ParseGoogleSpeechAPIResponse(recognized);

                    OnSpeechRecognized(new SpeechRecognizedEventArgs(e.PrevTalker, recognized));
                };
            action.BeginInvoke(null, null);
        }

        public static void Start(INetworkChatCodec codec)
        {
            _buffer = new byte[0];
            _codec = codec;
            AudioIO.MergedTick += AudioIO_MergedTick;
        }
        public static void Stop()
        {
            AudioIO.MergedTick -= AudioIO_MergedTick;
            _codec = null;
        }
    }
}