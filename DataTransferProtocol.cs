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
    }
}
