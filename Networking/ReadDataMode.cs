namespace TM14.Networking
{
    /// <summary>
    /// A list of options used to determine how the <see cref="TcpClient"/> will read data.
    /// </summary>
    public enum ReadDataMode
    {
        /// <summary>
        /// This mode is used to make the <see cref="TcpClient"/> start its own read data loop.
        /// </summary>
        Internally,

        /// <summary>
        /// This mode is used to make the <see cref="TcpClient"/> wait for calls to <see cref="TcpClient.ReadData"/> to read data.
        /// </summary>
        Externally
    }
}
