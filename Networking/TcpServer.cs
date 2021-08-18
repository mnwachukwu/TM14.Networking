using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using TM14.Networking.Events;

namespace TM14.Networking
{
    /// <summary>
    /// A wrapper class around the <see cref="TcpListener"/> class used to handle connected clients and sending/receiving data.
    /// </summary>
    public class TcpServer
    {
        /// <summary>
        /// The underlying <see cref="TcpListener"/> this object uses to communicate with clients.
        /// </summary>
        private readonly TcpListener server;

        /// <summary>
        /// An event which is invoked whenever the networking library has a message.
        /// </summary>
        public event EventHandler<HasMessageEventArgs> HasMessage;

        /// <summary>
        /// An event which is invoked whenever the server receives data and a packet becomes available for handling.
        /// </summary>
        public event EventHandler<HasPacketEventArgs> HasPacket;

        /// <summary>
        /// An event which is invoked whenever a client is connected.
        /// </summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;

        /// <summary>
        /// An event which is invoked whenever a client is disconnected.
        /// </summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>
        /// A list of connected clients.
        /// </summary>
        public List<System.Net.Sockets.TcpClient> ConnectedClients { get; }

        /// <summary>
        /// IPs to exclude when accepting connections.
        /// </summary>
        public List<string> ExcludedIps { get; set; }

        /// <summary>
        /// Determines if the server is currently listening for connections.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Contains a collection of <see cref="ReadData"/> threads, indexed by the client utilizing the thread.
        /// </summary>
        private Dictionary<System.Net.Sockets.TcpClient, Thread> ReadDataThread { get; }

        /// <summary>
        /// A buffer for reading packets in an orderly fashion.
        /// </summary>
        private PacketBuffer PacketBuffer { get; }

        /// <summary>
        /// Instantiates a server and prepares it to listen for connections.
        /// </summary>
        /// <param name="ip">The IP address of the computer the server program is running on.</param>
        /// <param name="port">The port on which the server program is listening.</param>
        public TcpServer(string ip, int port)
        {
            var localAddr = IPAddress.Parse(ip);

            server = new TcpListener(localAddr, port);
            ConnectedClients = new List<System.Net.Sockets.TcpClient>();
            ReadDataThread = new Dictionary<System.Net.Sockets.TcpClient, Thread>();
            PacketBuffer = new PacketBuffer();
            ExcludedIps = new List<string>();
        }

        /// <summary>
        /// Adds an IP to the IP exclusion list.
        /// </summary>
        /// <param name="ip">IP to exclude.</param>
        public void Exclude(string ip)
        {
            ExcludedIps.Add(ip);
        }

        /// <summary>
        /// Adds a range of IPs to the IP exclusion list.
        /// </summary>
        /// <param name="ips">List of IPs to exclude.</param>
        public void Exclude(IEnumerable<string> ips)
        {
            if (ExcludedIps.Any())
            {
                ExcludedIps.AddRange(ips);
            }
            else
            {
                ExcludedIps = ips.ToList();
            }
        }

        /// <summary>
        /// Closes a connection to the client and removes them from the list of tracked clients.
        /// </summary>
        /// <param name="client">Client to disconnect.</param>
        public void DisconnectClient(System.Net.Sockets.TcpClient client)
        {
            var args = new ClientDisconnectedEventArgs { Client = client };
            
            OnClientDisconnected(args);

            if (client.Connected)
            {
                client.Close();
            }

            ConnectedClients.Remove(client);

            if (ReadDataThread.ContainsKey(client))
            {
                ReadDataThread[client].Abort();
                ReadDataThread.Remove(client);
            }
        }

        /// <summary>
        /// A method containing a loop which listens for new connections. Once a connection has been made,
        /// a new thread is spun up to handle data from that client and the method loops back to listening
        /// for a new connection.
        /// </summary>
        private void ListenerLoop()
        {
            Message("Server is now listening for connections.");

            while (IsActive)
            {
                var client = server.AcceptTcpClient();
                var ipAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                if (!ExcludedIps.Contains(ipAddress))
                {
                    var args = new ClientConnectedEventArgs { Client = client };

                    OnClientConnected(args);
                    ConnectedClients.Add(client);
                    ReadDataThread[client] = new Thread(ReadData);
                    ReadDataThread[client].Start(client);
                }
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
                    var data = Encoding.Unicode.GetString(bytes, 0, i);
                    var keyBytes = Convert.FromBase64String(DataTransferProtocol.SecretKey);

                    PacketBuffer.Enqueue(data);

                    while (PacketBuffer.Queue.Any())
                    {
                        var decryptedPacketString = AesHmacCrypto.SimpleDecrypt(PacketBuffer.Queue.Dequeue(), keyBytes, keyBytes);
                        HandleData(client, decryptedPacketString);
                    }
                }

                DisconnectClient(client);
            }
            catch (ThreadAbortException)
            {
                ReadDataThread.Remove(client);
            }
            catch (Exception e)
            {
                DisconnectClient(client);
                Debug.WriteLine($"Exception: {e}");
                // TODO: Send a message to the client here
            }
        }

        /// <summary>
        /// Calls the method containing the listen loop after preparing the server to start listening for connections.
        /// This method also stops the server from listening after the listen loop has been broken.
        /// </summary>
        public void StartListener()
        {
            server.Start();
            IsActive = true;

            try
            {
                ListenerLoop();
            }
            catch (SocketException e)
            {
                Debug.Write($"SocketException: {e.Message}");
            }

            server.Stop();
            IsActive = false;
        }

        /// <summary>
        /// Breaks the listen loop.
        /// </summary>
        public void StopListener()
        {
            IsActive = false;
        }

        /// <summary>
        /// Determines if a particular client is connected to the server.
        /// </summary>
        /// <param name="client">The client to check.</param>
        /// <returns>True if connected, false otherwise.</returns>
        public bool IsClientConnected(System.Net.Sockets.TcpClient client)
        {
            return client != null && client.Connected;
        }

        /// <summary>
        /// Sends data to a specified client.
        /// </summary>
        /// <param name="client">The client to send data to.</param>
        /// <param name="data">The packet of data to send.</param>
        public void SendDataTo(System.Net.Sockets.TcpClient client, Packet data)
        {
            if (IsClientConnected(client))
            {
                var stream = client.GetStream();
                var keyBytes = Convert.FromBase64String(DataTransferProtocol.SecretKey);
                var encryptedPacketString = AesHmacCrypto.SimpleEncrypt(data.ToString(), keyBytes, keyBytes);
                var dataBytes = Encoding.Unicode.GetBytes(encryptedPacketString + DataTransferProtocol.PacketDelimiter);

                try
                {
                    stream.Write(dataBytes, 0, dataBytes.Length);
                }
                catch (Exception e)
                {
                    DisconnectClient(client);
                    Debug.WriteLine($"Exception: {e}");
                }
            }
        }

        /// <summary>
        /// Sends data to a list of specified clients.
        /// </summary>
        /// <param name="clients">The list of clients to send data to.</param>
        /// <param name="data">The packet of data to send.</param>
        public void SendDataTo(IEnumerable<System.Net.Sockets.TcpClient> clients, Packet data)
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
        /// Builds a <see cref="HasPacketEventArgs"/> to raise with an event invocation.
        /// </summary>
        /// <param name="sender">The client that sent the packet.</param>
        /// <param name="data">The string representation of the data packet.</param>
        private void HandleData(System.Net.Sockets.TcpClient sender, string data)
        {
            var args = new HasPacketEventArgs
            {
                Sender = sender,
                Packet = JsonConvert.DeserializeObject<Packet>(data)
            };

            OnHasPacket(args);
        }

        /// <summary>
        /// Builds a <see cref="HasMessageEventArgs"/> to raise with an event invocation.
        /// </summary>
        /// <param name="message">The message.</param>
        private void Message(string message)
        {
            var args = new HasMessageEventArgs
            {
                Message = message,
                TimeStamp = DateTime.Now
            };

            OnHasMessage(args);
        }

        /// <summary>
        /// Invokes an event containing a string message.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        private void OnHasMessage(HasMessageEventArgs e)
        {
            var handler = HasMessage;
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Invokes an event containing a <see cref="Packet"/>.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        private void OnHasPacket(HasPacketEventArgs e)
        {
            var handler = HasPacket;
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Invokes an event containing a disconnecting <see cref="System.Net.Sockets.TcpClient"/>.
        /// </summary>
        /// <param name="e">The event arguements.</param>
        private void OnClientConnected(ClientConnectedEventArgs e)
        {
            var handler = ClientConnected;

            Message($"Client {e.Client.Client.RemoteEndPoint} connected.");
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Invokes an event containing a disconnecting <see cref="System.Net.Sockets.TcpClient"/>.
        /// </summary>
        /// <param name="e">The event arguements.</param>
        private void OnClientDisconnected(ClientDisconnectedEventArgs e)
        {
            var handler = ClientDisconnected;

            Message($"Client {e.Client.Client.RemoteEndPoint} disconnected.");
            handler?.Invoke(this, e);
        }
    }
}
