using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Network
{
    public class ExceptionArgs : EventArgs
    {
        private string _message;
        public ExceptionArgs(string tmp)
        {
            this._message = tmp;
        }
        public string Message
        {
            get
            {
                return _message;
            }
        }
    }
}
