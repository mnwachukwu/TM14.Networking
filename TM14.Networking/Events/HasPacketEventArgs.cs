using System;

namespace TM14.Networking.Events
{
    /// <summary>
    /// Arguments to raise with a HasPacket event.
    /// </summary>
    public class HasPacketEventArgs : EventArgs
    {
        /// <summary>
        /// The sender of the packet. If null, the sender was the server.
        /// </summary>
        public System.Net.Sockets.TcpClient Sender { get; set; }

        /// <summary>
        /// The packet of data that was received.
        /// </summary>
        public Packet Packet { get; set; }
    }
}
