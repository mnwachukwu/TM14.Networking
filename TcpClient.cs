using System;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

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
        private readonly System.Net.Sockets.TcpClient client;

        /// <summary>
        /// An event which is invoked whenever the networking pipeline has a message.
        /// </summary>
        public event EventHandler<HasConsoleMessageEventArgs> HasConsoleMessage;

        /// <summary>
        /// An event which is invoked whenever the server sends data.
        /// </summary>
        public event EventHandler<HasHandledPacketEventArgs> HasHandledPacket;

        /// <summary>
        /// Determines if the underlying <see cref="System.Net.Sockets.TcpClient"/> is connected.
        /// </summary>
        public bool IsConnected => client != null && client.Connected;

        /// <summary>
        /// Determines if the <see cref="TcpClient"/> will read messages in its own thread or if the client
        /// will wait for a calling thread to read messages.
        /// </summary>
        private ReadDataMode ReadDataMode { get; }

        /// <summary>
        /// Intantiates a client and connects to the specified IP on the specified port.
        /// This method also starts a new thread which reads messages from the server.
        /// </summary>
        /// <param name="serverIp">The IP to connect to.</param>
        /// <param name="port">The port to connect over.</param>
        /// <param name="readMessageMode">Should this client read messages in its own thread (internally),
        ///                               or will it be processed on some other thread (externally)?</param>
        public TcpClient(string serverIp, int port, ReadDataMode readMessageMode = ReadDataMode.Internally)
        {
            client = new System.Net.Sockets.TcpClient(serverIp, port);
            ReadDataMode = readMessageMode;

            if (readMessageMode == ReadDataMode.Internally)
            {
                var t = new Thread(ReadDataInternally);
                t.Start();
            }
        }

        /// <summary>
        /// Sends data to the server.
        /// </summary>
        /// <param name="data">The packet of data to send.</param>
        public void SendData(Packet data)
        {
            var stream = client.GetStream();
            var dataBytes = Encoding.ASCII.GetBytes(data.ToString());
            stream.Write(dataBytes, 0, dataBytes.Length);
        }

        /// <summary>
        /// Handles reading packets from the server, passing it to the HandleData method.
        /// <remarks> This method will block the calling thread and intended to be used by the
        ///           <see cref="TcpClient"/> class. </remarks>
        /// </summary>
        private void ReadDataInternally()
        {
            if (ReadDataMode != ReadDataMode.Internally)
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
                    var data = Encoding.ASCII.GetString(bytes, 0, i);
                    HandleData(data);
                }
            }
            catch (Exception e)
            {
                ConsoleMessage($"Exception: {e}");
                // TODO: Display a message to the user here
                client.Close();
            }
        }

        /// <summary>
        /// Handles reading packets from the server, passing it to the HandleData method.
        /// <remarks> This method will not block the calling thread and is intended to be used outside
        ///           of the <see cref="TcpClient"/> class inside a loop. </remarks>
        /// </summary>
        public void ReadData()
        {
            if (ReadDataMode != ReadDataMode.Externally)
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
                    var data = Encoding.ASCII.GetString(bytes, 0, i);
                    HandleData(data);
                }
            }
            catch (Exception e)
            {
                ConsoleMessage($"Exception: {e}");
                // TODO: Display a message to the user here
                client.Close();
            }
        }

        /// <summary>
        /// Builds a <see cref="HasHandledPacketEventArgs"/> to raise with an event invocation.
        /// </summary>
        /// <param name="data">The string representation of the data packet.</param>
        private void HandleData(string data)
        {
            var args = new HasHandledPacketEventArgs
            {
                Sender = null,
                Packet = JsonConvert.DeserializeObject<Packet>(data)
            };
            OnHasHandledPacket(args);
        }

        /// <summary>
        /// Closes the connection to the server.
        /// </summary>
        public void Close()
        {
            client.Close();
        }

        /// <summary>
        /// Builds a <see cref="HasConsoleMessageEventArgs"/> to raise with an event invocation.
        /// </summary>
        /// <param name="message">The message.</param>
        internal void ConsoleMessage(string message)
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
