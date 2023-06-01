using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TM14.Networking
{
    /// <summary>
    /// A representation of a data packet that has a unique identifier, a header, and data.
    /// </summary>
    public class Packet
    {
        /// <summary>
        /// The unique identifier of the packet.
        /// </summary>
        public string Guid { get; set; }

        /// <summary>
        /// The header of the packet. This typically represents the kind of data this packet contains.
        /// </summary>
        public string Header { get; set; }

        /// <summary>
        /// The data within the packet.
        /// </summary>
        public List<string> Data { get; set; }

        /// <summary>
        /// Constructs a packet with a header and no data.
        /// </summary>
        /// <param name="header">The packet header.</param>
        public Packet(string header)
        {
            Guid = System.Guid.NewGuid().ToString();
            Header = header;
            Data = new List<string>();
        }

        /// <summary>
        /// Constructs a packet with a header and string data.
        /// </summary>
        /// <param name="header">The packet header.</param>
        /// <param name="data">The packet data.</param>
        [JsonConstructor]
        public Packet(string header, params string[] data)
        {
            Guid = System.Guid.NewGuid().ToString();
            Header = header;
            Data = data.ToList();
        }

        /// <summary>
        /// Constructs a packet with a header and object data.
        /// </summary>
        /// <param name="header">The packet header.</param>
        /// <param name="data">The packet data.</param>
        public Packet(string header, params object[] data)
        {
            Guid = System.Guid.NewGuid().ToString();
            Header = header;
            Data = data.Select(JsonConvert.SerializeObject).ToList();
        }

        /// <summary>
        /// Adds string data to the packet.
        /// </summary>
        /// <param name="data">The packet data.</param>
        public void AddData(params string[] data)
        {
            if (data != null)
            {
                Data = Data.Concat(data).ToList();
            }
        }

        /// <summary>
        /// Adds object data to the packet.
        /// </summary>
        /// <param name="data">The packet data.</param>
        public void AddData(params object[] data)
        {
            var dataStrings = data.Select(JsonConvert.SerializeObject).ToArray();
            AddData(dataStrings);
        }

        /// <summary>
        /// Returns a JSON string representation of the packet.
        /// </summary>
        /// <returns>A string representation of the packet.</returns>
        public new string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
