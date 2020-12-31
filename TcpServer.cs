using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace TM14.Networking
{
    /// <summary>
    /// A simplified server made to handle connected clients and sending/receiving data.
    /// </summary>
    public class TcpServer
    {
        /// <summary>
        /// The underlying <see cref="TcpListener"/> this object uses to communicate with clients.
        /// </summary>
        private readonly TcpListener server;

        /// <summary>
        /// An event which is invoked whenever the networking pipeline has a message.
        /// </summary>
        public event EventHandler<HasConsoleMessageEventArgs> HasConsoleMessage;

        /// <summary>
        /// An event which is invoked whenever the server sends data.
        /// </summary>
        public event EventHandler<HasHandledPacketEventArgs> HasHandledPacket;

        /// <summary>
        /// A list of connected clients.
        /// </summary>
        public List<System.Net.Sockets.TcpClient> ConnectedClients { get; }

        /// <summary>
        /// Determines if the server is currently listening for connections.
        /// </summary>
        private bool IsActive { get; set; }

        /// <summary>
        /// Instantiates a server listener which listens for connections on the specified port.
        /// </summary>
        /// <param name="ip">The IP address of the computer the server program is running on.</param>
        /// <param name="port">The port on which the server program is listening.</param>
        public TcpServer(string ip, int port)
        {
            var localAddr = IPAddress.Parse(ip);
            IsActive = true;
            ConnectedClients = new List<System.Net.Sockets.TcpClient>();
            server = new TcpListener(localAddr, port);
        }

        /// <summary>
        /// Closes a connection to the client and removes them from the list of tracked clients.
        /// </summary>
        /// <param name="client"></param>
        public void DisconnectClient(System.Net.Sockets.TcpClient client)
        {
            ConsoleMessage($"Client {client.Client.RemoteEndPoint} disconnected.");
            client.Close();
            ConnectedClients.Remove(client);
        }

        /// <summary>
        /// A method contianing an infinite loop which listens for new connections. Once a connection has been made,
        /// a new thread is spun up to handle data from that client and the method loops back to listening for a new
        /// connection.
        /// </summary>
        private void ListenerLoop()
        {
            ConsoleMessage("Server is now listening for connections.");

            while (IsActive)
            {
                var client = server.AcceptTcpClient();
                ConnectedClients.Add(client);
                ConsoleMessage($"Client connected from {client.Client.RemoteEndPoint}.");
                var t = new Thread(ReadData);
                t.Start(client);
            }
        }

        /// <summary>
        /// Handles reading data from a client, passing it to the HandleData method.
        /// </summary>
        /// <param name="obj">The client whose data should be read.</param>
        private void ReadData(object obj)
        {
            var client = (System.Net.Sockets.TcpClient)obj;
            var stream = client.GetStream();
            var bytes = new byte[DataTransferProtocol.BufferSize];
            try
            {
                int i;
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    var data = Encoding.ASCII.GetString(bytes, 0, i);
                    HandleData(client, data);
                }
                DisconnectClient(client);
            }
            catch (Exception e)
            {
                ConsoleMessage($"Exception: {e}");
                // TODO: Send a message to the client here
                DisconnectClient(client);
            }
        }

        /// <summary>
        /// Builds a <see cref="HasHandledPacketEventArgs"/> to raise with an event invocation.
        /// </summary>
        /// <param name="sender">The client that sent the packet.</param>
        /// <param name="data">The string representation of the data packet.</param>
        private void HandleData(System.Net.Sockets.TcpClient sender, string data)
        {
            var args = new HasHandledPacketEventArgs
            {
                Sender = sender,
                Packet = JsonConvert.DeserializeObject<Packet>(data)
            };
            OnHasHandledPacket(args);
        }

        /// <summary>
        /// Calls the method containing the listen loop after preparing the server to start listening for connections.
        /// This method also stops the server from listening after the listen loop has been broken.
        /// </summary>
        public void StartListener()
        {
            server.Start();

            try
            {
                ListenerLoop();
            }
            catch (SocketException e)
            {
                ConsoleMessage($"SocketException: {e}");
            }

            server.Stop();
        }

        /// <summary>
        /// Breaks the listen loop.
        /// </summary>
        public void StopListener()
        {
            IsActive = false;
        }

        /// <summary>
        /// Sends data to a specified client.
        /// </summary>
        /// <param name="client">The client to send data to.</param>
        /// <param name="data">The packet of data to send.</param>
        public void SendDataTo(System.Net.Sockets.TcpClient client, Packet data)
        {
            var stream = client.GetStream();
            var dataBytes = Encoding.ASCII.GetBytes(data.ToString());
            stream.Write(dataBytes, 0, dataBytes.Length);
        }

        /// <summary>
        /// Sends data to a list of specified clients.
        /// </summary>
        /// <param name="clients">The list of clients to send data to.</param>
        /// <param name="data">The packet of data to send.</param>
        public void SendDataTo(List<System.Net.Sockets.TcpClient> clients, Packet data)
        {
            foreach (var client in clients)
            {
                SendDataTo(client, data);
            }
        }

        /// <summary>
        /// Sends data to every connected client.
        /// </summary>
        /// <param name="data">The packet of data to send.</param>
        public void SendDataToAll(Packet data)
        {
            SendDataTo(ConnectedClients, data);
        }

        /// <summary>
        /// Builds a <see cref="HasConsoleMessageEventArgs"/> to raise with an event invocation.
        /// </summary>
        /// <param name="message">The message.</param>
        public void ConsoleMessage(string message)
        {
            var args = new HasConsoleMessageEventArgs
            {
                Message = message,
                TimeStamp = DateTime.Now
            };
            OnHasConsoleMessage(args);
        }

        /// <summary>
        /// Invokes an event containing a string message.
        /// </summary>
        /// <param name="e">The event arguements.</param>
        protected virtual void OnHasConsoleMessage(HasConsoleMessageEventArgs e)
        {
            var handler = HasConsoleMessage;
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Invokes an event containing a <see cref="Packet"/>.
        /// </summary>
        /// <param name="e">The event arguements.</param>
        protected virtual void OnHasHandledPacket(HasHandledPacketEventArgs e)
        {
            var handler = HasHandledPacket;
            handler?.Invoke(this, e);
        }
    }
}
