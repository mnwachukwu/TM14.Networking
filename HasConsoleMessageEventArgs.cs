using System;

namespace TM14.Networking
{
    /// <summary>
    /// Arguments to raise with a HasConsoleMessage event.
    /// </summary>
    public class HasConsoleMessageEventArgs : EventArgs
    {
        /// <summary>
        /// The message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The time at which the event was raised.
        /// </summary>
        public DateTime TimeStamp { get; set; }
    }
}
