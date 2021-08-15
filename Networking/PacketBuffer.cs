using System.Collections.Generic;

namespace TM14.Networking
{
    internal class PacketBuffer
    {
        private string internalBuffer;

        public Queue<string> Queue { get; set; }

        internal PacketBuffer()
        {
            Queue = new Queue<string>();
        }

        internal void Enqueue(string buffer)
        {
            internalBuffer += buffer;

            while (internalBuffer.Contains(DataTransferProtocol.PacketDelimiter.ToString()))
            {
                var sliceLength = internalBuffer.IndexOf(DataTransferProtocol.PacketDelimiter);
                var legibleSlice = internalBuffer.Substring(0, sliceLength);

                Queue.Enqueue(legibleSlice);
                internalBuffer = internalBuffer.Remove(0, sliceLength + 1);
            }
        }
    }
}
