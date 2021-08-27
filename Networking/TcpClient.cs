using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using TM14.Networking.Events;

namespace TM14.Networking
{
    /// <summary>
    /// A wrapper class around the <see cref="System.Net.Sockets.TcpClient"/> class used to handle connecting to a server and sending/receiving data.
    /// </summary>
    public class TcpClient
    {
        /// <summary>
        /// The underlying <see cref="System.Net.Sockets.TcpClient"/> this object utilizes.
        /// </summary>
        private System.Net.Sockets.TcpClient client;

        /// <summary>
        /// The thread used to read data.
        /// </summary>
        private Thread readDataThread;

        /// <summary>
        /// A buffer for reading packets in an orderly fashion.
        /// </summary>
        private readonly PacketBuffer packetBuffer;

        /// <summary>
        /// The IP address of the server the client will connect to.
        /// </summary>
        private readonly string serverIp;

        /// <summary>
        /// The port which the client will communicate on.
        /// </summary>
        private readonly int port;

        /// <summary>
        /// Determines if the underlying <see cref="System.Net.Sockets.TcpClient"/> is connected.
        /// </summary>
        public bool IsConnected => client != null && client.Connected;

        /// <summary>
        /// An event which is invoked whenever a connection to the server is established.
        /// </summary>
        public event EventHandler<EventArgs> Connected;

        /// <summary>
        /// An event which is invoked whenever a connection to the server is closed.
        /// </summary>
        public event EventHandler<EventArgs> Disconnected;

        /// <summary>
        /// An event which is invoked whenever a connection attempt has failed.
        /// </summary>
        public event EventHandler<ConnectionFailedEventArgs> ConnectionFailed;

        /// <summary>
        /// An event which is invoked whenever the library has a message.
        /// </summary>
        public event EventHandler<HasMessageEventArgs> HasMessage;

        /// <summary>
        /// An event which is invoked whenever the client receives data and a packet becomes available for handling.
        /// </summary>
        public event EventHandler<HasPacketEventArgs> HasPacket;

        /// <summary>
        /// An event which is invoked whenever the library catches an exception in <see cref="ReadData"/> or <see cref="SendData"/>.
        /// </summary>
        public event EventHandler<HasCaughtExceptionEventArgs> HasCaughtException;

        /// <summary>
        /// Instantiates a client and prepares it to connect to a server.
        /// </summary>
        /// <param name="serverIp">The IP to connect to.</param>
        /// <param name="port">The port to connect over.</param>
        public TcpClient(string serverIp, int port)
        {
            this.serverIp = serverIp;
            this.port = port;
            packetBuffer = new PacketBuffer();
        }

        /// <summary>
        /// Sends data to the server.
        /// </summary>
        /// <param name="data">The packet of data to send.</param>
        public void SendData(Packet data)
        {
            if (IsConnected)
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
                    // TODO: Display a message to the user here

                    OnHasCaughtException(e, DateTime.Now);
                    Disconnect();
                }
            }
        }

        /// <summary>
        /// Handles reading data from the server, passing it to the HandleData method.
        /// <remarks>
        /// This method will block the calling thread and intended to be used by the
        /// <see cref="TcpClient"/> class.
        /// </remarks>
        /// </summary>
        private void ReadData()
        {
            if (!client.Connected)
            {
                return;
            }

            var stream = client.GetStream();
            var bytes = new byte[DataTransferProtocol.BufferSize];

            try
            {
                int i;

                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    var data = Encoding.Unicode.GetString(bytes, 0, i);
                    var keyBytes = Convert.FromBase64String(DataTransferProtocol.SecretKey);

                    packetBuffer.Enqueue(data);

                    while (packetBuffer.Queue.Any())
                    {
                        var decryptedPacketString = AesHmacCrypto.SimpleDecrypt(packetBuffer.Queue.Dequeue(), keyBytes, keyBytes);

                        HandleData(decryptedPacketString);
                    }
                }

                Disconnect();
            }
            catch (Exception e)
            {
                // TODO: Display a message to the user here

                OnHasCaughtException(e, DateTime.Now);
                Disconnect();
            }
        }

        /// <summary>
        /// Deserializes a string into a <see cref="Packet"/> that will be bubbled up to <see cref="HasPacket"/>.
        /// </summary>
        /// <param name="data">The string representation of the packet to deserialize.</param>
        private void HandleData(string data)
        {
            var packet = JsonConvert.DeserializeObject<Packet>(data);

            OnHasPacket(null, packet);
        }

        /// <summary>
        /// Attempts a connection to the server.
        /// </summary>
        public void Connect()
        {
            try
            {
                client = new System.Net.Sockets.TcpClient(serverIp, port);
                OnConnect();
                readDataThread = new Thread(ReadData);
                readDataThread.Start();
            }
            catch (SocketException e)
            {
                OnConnectionFailed(e);
            }
        }

        /// <summary>
        /// Closes the connection to the server.
        /// </summary>
        public void Disconnect()
        {
            if (client != null && client.Connected)
            {
                client.Close();
                OnDisconnect();
            }
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
        /// Invokes an event signifying that the client has been connected.
        /// </summary>
        private void OnConnect()
        {
            var handler = Connected;

            Message("Connected to server.");
            handler?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes an event signifying that the client has been disconnected.
        /// </summary>
        private void OnDisconnect()
        {
            var handler = Disconnected;

            Message("Disconnected from server.");
            handler?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes an event signifying that the client failed to connect to the server.
        /// The event contains the underlying <see cref="SocketException"/> which caused the failure.
        /// </summary>
        /// <param name="exception">The socket exception to contain.</param>
        private void OnConnectionFailed(SocketException exception)
        {
            var handler = ConnectionFailed;
            var eventArgs = new ConnectionFailedEventArgs
            {
                SocketException = exception
            };

            Message("Failed to connect to server.");
            handler?.Invoke(this, eventArgs);
            Debug.WriteLine($"SocketException: {exception.Message}");
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
