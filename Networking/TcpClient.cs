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
    /// Wrapper for the <see cref="System.Net.Sockets.TcpClient"/> class to make utilizing it simpler.
    /// </summary>
    public class TcpClient
    {
        /// <summary>
        /// The underlying <see cref="System.Net.Sockets.TcpClient"/> this object utilizes.
        /// </summary>
        private System.Net.Sockets.TcpClient client;

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
        /// An event which is invoked whenever the networking library has a message.
        /// </summary>
        public event EventHandler<HasMessageEventArgs> HasMessage;

        /// <summary>
        /// An event which is invoked whenever the server sends data.
        /// </summary>
        public event EventHandler<HasPacketEventArgs> HasPacket;

        /// <summary>
        /// Determines if the underlying <see cref="System.Net.Sockets.TcpClient"/> is connected.
        /// </summary>
        public bool IsConnected => client != null && client.Connected;

        /// <summary>
        /// Determines if the <see cref="TcpClient"/> will read data in its own thread or if the client
        /// will wait for a calling thread to read data.
        /// </summary>
        private ReadDataMode ReadDataMode { get; }

        /// <summary>
        /// A buffer for reading packets in an orderly fashion.
        /// </summary>
        private PacketBuffer PacketBuffer { get; }

        /// <summary>
        /// The IP address of the server the client will connect to.
        /// </summary>
        private string ServerIp { get; }

        /// <summary>
        /// The port which the client will communicate on.
        /// </summary>
        private int Port { get; }

        /// <summary>
        /// Instantiates a client and prepares it to connect to a server.
        /// </summary>
        /// <param name="serverIp">The IP to connect to.</param>
        /// <param name="port">The port to connect over.</param>
        /// <param name="readDataMode">
        /// Should this client read data in its own thread (internally),
        /// or will it be processed on some other thread (externally)?
        /// </param>
        public TcpClient(string serverIp, int port, ReadDataMode readDataMode = ReadDataMode.Internally)
        {
            ServerIp = serverIp;
            Port = port;
            ReadDataMode = readDataMode;
            PacketBuffer = new PacketBuffer();
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
                    Disconnect();
                    Debug.WriteLine(e);
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
        private void ReadDataInternally()
        {
            if (ReadDataMode != ReadDataMode.Internally)
            {
                return;
            }

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

                    PacketBuffer.Enqueue(data);

                    while (PacketBuffer.Queue.Any())
                    {
                        var decryptedPacketString = AesHmacCrypto.SimpleDecrypt(PacketBuffer.Queue.Dequeue(), keyBytes, keyBytes);
                        HandleData(decryptedPacketString);
                    }
                }
            }
            catch (Exception e)
            {
                Disconnect();
                Debug.WriteLine($"Exception: {e}");
                // TODO: Display a message to the user here
            }
        }

        /// <summary>
        /// Handles reading data from the server, passing it to the HandleData method.
        /// <remarks>
        /// This method will not block the calling thread and is intended to be used outside
        /// of the <see cref="TcpClient"/> class inside a loop.
        /// </remarks>
        /// </summary>
        public void ReadData()
        {
            if (ReadDataMode != ReadDataMode.Externally)
            {
                return;
            }

            if (!client.Connected)
            {
                return;
            }

            var stream = client.GetStream();
            var bytes = new byte[DataTransferProtocol.BufferSize];

            try
            {
                if (stream.DataAvailable)
                {
                    var i = stream.Read(bytes, 0, bytes.Length);
                    var data = Encoding.Unicode.GetString(bytes, 0, i);
                    var keyBytes = Convert.FromBase64String(DataTransferProtocol.SecretKey);

                    PacketBuffer.Enqueue(data);

                    while (PacketBuffer.Queue.Any())
                    {
                        var decryptedPacketString = AesHmacCrypto.SimpleDecrypt(PacketBuffer.Queue.Dequeue(), keyBytes, keyBytes);
                        HandleData(decryptedPacketString);
                    }
                }
            }
            catch (Exception e)
            {
                Disconnect();
                Debug.WriteLine($"Exception: {e}");
                // TODO: Display a message to the user here
                // TODO: When reading data externally, this needs to stop the external reader process (such as a timer)
            }
        }

        /// <summary>
        /// Builds a <see cref="HasPacketEventArgs"/> to raise with an event invocation.
        /// </summary>
        /// <param name="data">The string representation of the data packet.</param>
        private void HandleData(string data)
        {
            var args = new HasPacketEventArgs
            {
                Sender = null,
                Packet = JsonConvert.DeserializeObject<Packet>(data)
            };

            OnHasPacket(args);
        }

        /// <summary>
        /// Attempts a connection to the server.
        /// </summary>
        public void Connect()
        {
            try
            {
                client = new System.Net.Sockets.TcpClient(ServerIp, Port);
                OnConnect(null);

                if (ReadDataMode == ReadDataMode.Internally)
                {
                    var t = new Thread(ReadDataInternally);
                    t.Start();
                }
            }
            catch (SocketException e)
            {
                var args = new ConnectionFailedEventArgs
                {
                    SocketException = e
                };

                OnConnectionFailed(args);
                Debug.WriteLine($"SocketException: {e.Message}");
            }
        }

        /// <summary>
        /// Closes the connection to the server.
        /// </summary>
        public void Disconnect()
        {
            client.Close();
            OnDisconnect(null);
        }

        /// <summary>
        /// Builds a <see cref="HasMessageEventArgs"/> to raise with an event invocation.
        /// </summary>
        /// <param name="message">The message.</param>
        internal void Message(string message)
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
        /// Invokes an event signifying that the client has been connected.
        /// </summary>
        /// <param name="e"></param>
        private void OnConnect(EventArgs e)
        {
            var handler = Connected;
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Invokes an event signifying that the client has been disconnected.
        /// </summary>
        /// <param name="e"></param>
        private void OnDisconnect(EventArgs e)
        {
            var handler = Disconnected;
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Invokes an event signifying that the client failed to connect to the server.
        /// The event contains the underlying <see cref="SocketException"/> which caused the failure.
        /// </summary>
        /// <param name="e"></param>
        private void OnConnectionFailed(ConnectionFailedEventArgs e)
        {
            var handler = ConnectionFailed;
            handler?.Invoke(this, e);
        }
    }
}
