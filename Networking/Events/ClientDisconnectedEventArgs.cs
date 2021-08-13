using System;

namespace TM14.Networking.Events
{
    public class ClientDisconnectedEventArgs : EventArgs
    {
        public System.Net.Sockets.TcpClient Client { get; set; }
    }
}
