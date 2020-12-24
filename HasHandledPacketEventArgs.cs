using System;

namespace TM14.Networking
{
    /// <summary>
    /// Arguements to raise with a HasHandledPacket event.
    /// </summary>
    public class HasHandledPacketEventArgs : EventArgs
    {
        /// <summary>
        /// The sender of the packet. If null, the sender was the server.
        /// </summary>
        public System.Net.Sockets.TcpClient Sender { get; set; }

        /// <summary>
        /// The packet of data that was handled.
        /// </summary>
        public Packet Packet { get; set; }
    }
}
