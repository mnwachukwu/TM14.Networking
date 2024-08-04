using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        /// The Port the server will listen on.
        /// </summary>
        private readonly int localPort;

        /// <summary>
        /// A buffer for reading packets in an orderly fashion.
        /// </summary>
        private readonly Dictionary<System.Net.Sockets.TcpClient, PacketBuffer> packetBuffer;

        /// <summary>
        /// An event which is invoked whenever the library has a message.
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
        /// An event which is invoked whenever the library catches an exception in <see cref="ReadData"/> or <see cref="SendDataTo(System.Net.Sockets.TcpClient,Packet)"/>.
        /// </summary>
        public event EventHandler<HasCaughtExceptionEventArgs> HasCaughtException;

        /// <summary>
        /// A list of connected clients.
        /// </summary>
        public List<System.Net.Sockets.TcpClient> ConnectedClients { get; }

        /// <summary>
        /// IPs to exclude when accepting connections.
        /// </summary>
        public List<string> ExcludedIps { get; private set; }

        /// <summary>
        /// Determines if the server is currently listening for connections.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Instantiates a server and prepares it to listen for connections.
        /// </summary>
        /// <param name="ip">The IP address of the computer the server program is running on.</param>
        /// <param name="port">The port on which the server program is listening.</param>
        public TcpServer(int port)
        {
            server = new TcpListener(IPAddress.Any, port);
            localPort = port;
            ConnectedClients = new List<System.Net.Sockets.TcpClient>();
            packetBuffer = new Dictionary<System.Net.Sockets.TcpClient, PacketBuffer>();
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
            OnClientDisconnected(client);

            if (client.Connected)
            {
                client.Close();
            }

            ConnectedClients.Remove(client);

            if (packetBuffer.ContainsKey(client))
            {
                packetBuffer.Remove(client);
            }
        }

        /// <summary>
        /// A method containing a loop which listens for new connections. Once a connection has been made,
        /// a new thread is spun up to handle data from that client and the method loops back to listening
        /// for a new connection.
        /// </summary>
        private void ListenerLoop()
        {
            Message($"Server is now listening for connections on port {localPort}.");

            while (IsActive)
            {
                var client = server.AcceptTcpClient();
                var ipAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                if (ExcludedIps.Contains(ipAddress))
                {
                    // TODO: Send a message indicating that the client has been excluded
                }
                else
                {
                    OnClientConnected(client);
                    ConnectedClients.Add(client);
                    ReadData(client);
                }
            }
        }

        /// <summary>
        /// Handles reading data from a client, passing it to the HandleData method.
        /// </summary>
        /// <param name="client">The client whose data should be read.</param>
        private async void ReadData(System.Net.Sockets.TcpClient client)
        {
            var stream = client.GetStream();
            var bytes = new byte[DataTransferProtocol.BufferSize];

            try
            {
                int i;

                while ((i = await stream.ReadAsync(bytes, 0, bytes.Length)) != 0)
                {
                    var data = Encoding.Unicode.GetString(bytes, 0, i);
                    var keyBytes = Convert.FromBase64String(DataTransferProtocol.SecretKey);

                    packetBuffer[client].Enqueue(data);

                    while (packetBuffer[client].Queue.Any())
                    {
                        var decryptedPacketString = AesHmacCrypto.SimpleDecrypt(packetBuffer[client].Queue.Dequeue(), keyBytes, keyBytes);

                        HandleData(client, decryptedPacketString);
                    }
                }

                DisconnectClient(client);
            }
            catch (IOException e)
            {
                if (e.InnerException?.GetType() == typeof(SocketException))
                {
                    if (((SocketException)e.InnerException).SocketErrorCode != SocketError.ConnectionAborted)
                    {
                        // TODO: Send a message to the client here

                        OnHasCaughtException(e, DateTime.Now);
                        DisconnectClient(client);
                    }
                }
                else
                {
                    // TODO: Send a message to the client here

                    OnHasCaughtException(e, DateTime.Now);
                    DisconnectClient(client);
                }
            }
            catch (Exception e)
            {
                // TODO: Send a message to the client here

                OnHasCaughtException(e, DateTime.Now);
                DisconnectClient(client);
            }
        }

        /// <summary>
        /// Calls the internal method containing the listen loop after preparing the server to start listening for connections.
        /// This method will also stop the server from listening after the listen loop has been broken or on error.
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
                OnHasCaughtException(e, DateTime.Now);
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
                catch (IOException e)
                {
                    if (e.InnerException?.GetType() == typeof(SocketException))
                    {
                        if (((SocketException)e.InnerException).SocketErrorCode != SocketError.ConnectionAborted)
                        {
                            // TODO: Send a message to the client here

                            OnHasCaughtException(e, DateTime.Now);
                            DisconnectClient(client);
                        }
                    }
                    else
                    {
                        // TODO: Send a message to the client here

                        OnHasCaughtException(e, DateTime.Now);
                        DisconnectClient(client);
                    }
                }
                catch (Exception e)
                {
                    // TODO: Send a message to the client here

                    OnHasCaughtException(e, DateTime.Now);
                    DisconnectClient(client);
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
        /// Deserializes a string into a <see cref="Packet"/> that will be bubbled up to <see cref="HasPacket"/>.
        /// </summary>
        /// <param name="sender">The client that sent the packet.</param>
        /// <param name="data">The string representation of the packet to deserialize.</param>
        private void HandleData(System.Net.Sockets.TcpClient sender, string data)
        {
            var packet = JsonConvert.DeserializeObject<Packet>(data);

            OnHasPacket(sender, packet);
        }

        /// <summary>
        /// Creates a message that will be bubbled up to <see cref="HasMessage"/>.
        /// </summary>
        /// <param name="message">The message.</param>
        private void Message(string message)
        {
            OnHasMessage(message, DateTime.Now);
        }

        /// <summary>
        /// Invokes an event containing a message.
        /// </summary>
        /// <param name="message">The message to contain.</param>
        /// <param name="timeStamp">The time at which the message was sent.</param>
        private void OnHasMessage(string message, DateTime timeStamp)
        {
            var handler = HasMessage;
            var eventArgs = new HasMessageEventArgs
            {
                Message = message,
                TimeStamp = timeStamp
            };

            handler?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Invokes an event containing a <see cref="Packet"/>.
        /// </summary>
        /// <param name="sender">The client which sent the packet.</param>
        /// <param name="packet">The packet to contain.</param>
        private void OnHasPacket(System.Net.Sockets.TcpClient sender, Packet packet)
        {
            var handler = HasPacket;
            var eventArgs = new HasPacketEventArgs
            {
                Sender = sender,
                Packet = packet
            };

            handler?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Invokes an event containing a disconnecting <see cref="System.Net.Sockets.TcpClient"/>.
        /// </summary>
        /// <param name="client">The client to contain.</param>
        private void OnClientConnected(System.Net.Sockets.TcpClient client)
        {
            var handler = ClientConnected;
            var eventArgs = new ClientConnectedEventArgs
            {
                Client = client
            };

            Message($"Client {client.Client.RemoteEndPoint} connected.");
            handler?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Invokes an event containing a disconnecting <see cref="System.Net.Sockets.TcpClient"/>.
        /// </summary>
        /// <param name="client">The client to contain.</param>
        private void OnClientDisconnected(System.Net.Sockets.TcpClient client)
        {
            var handler = ClientDisconnected;
            var eventArgs = new ClientDisconnectedEventArgs
            {
                Client = client
            };

            Message($"Client {client.Client.RemoteEndPoint} disconnected.");
            handler?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Invokes an event containing an <see cref="Exception"/> that was caught.
        /// </summary>
        /// <param name="exception">The exception to contain.</param>
        /// <param name="timeStamp">The time at which the exception was thrown.</param>
        private void OnHasCaughtException(Exception exception, DateTime timeStamp)
        {
            var handler = HasCaughtException;
            var eventArgs = new HasCaughtExceptionEventArgs
            {
                Exception = exception,
                TimeStamp = timeStamp
            };

            handler?.Invoke(this, eventArgs);
            Debug.WriteLine($"Exception: {exception.Message}");
        }
    }
}
