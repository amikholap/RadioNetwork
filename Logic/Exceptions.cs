using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logic
{
    [Serializable]
    public class RNException : Exception
    {
        public RNException()
            : base() { }

        public RNException(string message)
            : base(message) { }
    }

    [Serializable]
    public class NetworkException : RNException
    {
    }
}
