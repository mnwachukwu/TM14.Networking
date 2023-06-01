using System;
using System.Net.Sockets;

namespace TM14.Networking.Events
{
    /// <summary>
    /// Arguments to raise with a ConnectionFailed event.
    /// </summary>
    public class ConnectionFailedEventArgs : EventArgs
    {
        /// <summary>
        /// The underlying <see cref="System.Net.Sockets.SocketException"/> which caused a connection failure.
        /// </summary>
        public SocketException SocketException { get; set; }
    }
}
