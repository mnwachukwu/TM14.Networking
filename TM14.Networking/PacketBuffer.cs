using System.Collections.Generic;

namespace TM14.Networking
{
    /// <summary>
    /// A class which helps buffer packet data to ensure data is parsed correctly and in its entirety.
    /// </summary>
    internal class PacketBuffer
    {
        /// <summary>
        /// The "under-the-hood" buffer that is used to read and prepare packets.
        /// </summary>
        private string internalBuffer;
        
        /// <summary>
        /// The list of buffered packets ready for processing and handling.
        /// </summary>
        internal Queue<string> Queue { get; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        internal PacketBuffer()
        {
            Queue = new Queue<string>();
        }

        /// <summary>
        /// Adds data to the buffer for parsing.
        /// </summary>
        /// <param name="data">The data to buffer.</param>
        internal void Enqueue(string data)
        {
            internalBuffer += data;

            while (internalBuffer.Contains(DataTransferProtocol.PacketDelimiter.ToString()))
            {
                var delimiterIndex = internalBuffer.IndexOf(DataTransferProtocol.PacketDelimiter);

                Queue.Enqueue(internalBuffer.Substring(0, delimiterIndex));
                internalBuffer = internalBuffer.Remove(0, delimiterIndex + 1);
            }
        }
    }
}
