namespace TM14.Networking
{
    /// <summary>
    /// Establishes the set of protocols that the server and all clients must be aware of in order to transfer data without errors.
    /// </summary>
    public static class DataTransferProtocol
    {
        /// <summary>
        /// Establishes a buffer size of approximately 1Mb.
        /// </summary>
        public const int BufferSize = 1000000;

        /// <summary>
        /// A character used to keep individual packets seperated.
        /// </summary>
        public const char PacketSeperator = '¤';

        /// <summary>
        /// A secret key to encrypt data sent with and to decrypt data receive by this library.
        /// </summary>
        public static string SecretKey { get; set; }
    }
}
