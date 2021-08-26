using System;

namespace TM14.Networking.Events
{
    /// <summary>
    /// Arguements to raise with a HasCaughtException event.
    /// </summary>
    public class HasCaughtExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// The exception that was caught.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// The time at which the exception was caught.
        /// </summary>
        public DateTime TimeStamp { get; set; }
    }
}
