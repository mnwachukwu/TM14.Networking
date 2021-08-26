using System;

namespace TM14.Networking.Events
{
    public class HasCaughtExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; set; }

        public DateTime TimeStamp { get; set; }
    }
}
