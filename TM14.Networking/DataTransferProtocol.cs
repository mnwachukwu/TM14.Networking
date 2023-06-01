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
        internal const int BufferSize = 1000000;

        /// <summary>
        /// A character used to keep individual packets seperated.
        /// </summary>
        internal const char PacketDelimiter = '¤';

        /// <summary>
        /// A secret key used to encrypt and decrypt data sent with and received by this library.
        /// </summary>
        internal static string SecretKey { get; private set; }

        /// <summary>
        /// Sets a secret key to be used by the library to encrypt and decrypt traffic.
        /// </summary>
        /// <param name="key"></param>
        public static void SetSecretKey(string key)
        {
            SecretKey = key;
        }
    }
}
