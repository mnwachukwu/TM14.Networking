# TM14.Networking

This library contains wrapper classes around the `System.Net.Sockets.TcpClient` and `System.Net.Sockets.TcpListener` classes to make rapid use of those classes to transfer data between applications.

In order to make quick usage of the data transfer functionality, a `Packet` implementation has been included so that structured data contracts can be created between a client and server program.

It is written against .Net Standard 2.0 so that it can be used in your .Net Framework, .Net Core, and .Net 5 applications.

# Example Usage

## Client
```cs
// Initiate the TcpClient somewhere
var TcpClient = new TcpClient(ServerIp, ServerPort);
TcpClient.HasHandledPacket += TcpClient_HasHandledPacket;
```

### Receiving data
```cs
// Example TcpClient_HasHandledPacket Method
private static void TcpClient_HasHandledPacket(object sender, HasHandledPacketEventArgs e)
{
    HandlePacket(e.Packet);
}

// Example HandlePacket Method
internal static void HandlePacket(Packet packet)
{
    switch (packet.Header.ToLower())
    {
        case "a data header here":
            // Do something with the packet data
            break;
    }
}
```

### Sending data
```cs
// Example method to build and send data
public static void SendData(string packetHeader, params string[] packetData)
{
    var packet = new Packet(packetHeader, packetData);
    TcpClient.SendData(packet);
}
```

## Server
```cs
// Initiate the TcpServer somewhere
var TcpServer = new TcpServer(ServerIp, ServerPort);
TcpServer.HasHandledPacket += TcpServer_HasHandledPacket;
TcpServer.StartListener();
```

### Receiving data
```cs
// Example TcpServer_HasHandledPacket Method
private static void TcpServer_HasHandledPacket(object sender, HasHandledPacketEventArgs e)
{
    HandlePacket(e.Sender, e.Packet);
}

// Example HandlePacket Method
internal static void HandlePacket(System.Net.Sockets.TcpClient client, Packet packet)
{
    switch (packet.Header.ToLower())
    {
        case "a data header here":
            // Do something with the packet data
            break;
    }
}
```

### Sending data
```cs
// Example method to build and send data
public static void SendData(System.Net.Sockets.TcpClient client, string packetHeader, params string[] packetData)
{
    var packet = new Packet(packetHeader, packetData);
    TcpServer.SendDataTo(client, packet);
}
```
