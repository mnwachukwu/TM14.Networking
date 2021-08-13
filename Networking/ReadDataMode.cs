namespace TM14.Networking
{
    /// <summary>
    /// Determines if the <see cref="TcpClient"/> will read messages in its own thread or if the client
    /// will wait for a calling thread to read messages.
    /// </summary>
    public enum ReadDataMode
    {
        Internally,
        Externally
    }
}
