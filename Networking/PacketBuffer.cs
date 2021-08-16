using System.Collections.Generic;

namespace TM14.Networking
{
    internal class PacketBuffer
    {
        private string internalBuffer;

        internal Queue<string> Queue { get; }

        internal PacketBuffer()
        {
            Queue = new Queue<string>();
        }

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
