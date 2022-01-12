# TM14.Networking Quick Help

This library contains wrapper classes around the `System.Net.Sockets.TcpClient` and `System.Net.Sockets.TcpListener` classes to make rapid use of those classes to transfer data between applications.

In order to make quick usage of the data transfer functionality, a `Packet` class has been included so that structured data contracts can be created between a client and server program.

A `PacketBuffer` class is included to ensure every packet is sent and received reliably, with no malformed parts being parsed.

It is written against .Net Standard 2.0 so that it can be used in your .Net Framework, .Net Core, and .Net 5 applications.

This library makes use of JSON and as such, utilizes `Newtonsoft.Json` as a dependency from NuGet.

There is a detailed [documentation](https://networking.tm14.net/) website if you need more guidance. There, you can find a full guide on the [Classes](https://networking.tm14.net/a00051.html) and [Events](https://networking.tm14.net/a00052.html) this library provides.

This project is open source on [Github](https://github.com/mnwachukwu/TM14.Networking), if you're interested in looking at the code base. You can also find a download link to the .dll under the [Releases](https://github.com/mnwachukwu/TM14.Networking/releases) section on Github if you prefer to just use the compiled assembly in your project.

Copyright &copy; [Studio TM14](https://tm14.net/)

# Example Usage
## Client
### Initialize
```cs
// Initialize the TcpClient somewhere
var tcpClient = new TcpClient(ServerIp, ServerPort);
tcpClient.HasPacket += TcpClient_HasPacket;
tcpClient.Connect();
```

### Sending data
```cs
// Example method to build and send packets of data
public static void SendData(string packetHeader, params string[] packetData)
{
    var packet = new Packet(packetHeader, packetData);
    tcpClient.SendData(packet);
}
```

### Receiving data
```cs
// Example TcpClient_HasPacket event handler
private static void TcpClient_HasPacket(object sender, HasPacketEventArgs e)
{
    HandlePacket(e.Packet);
}

// Example HandlePacket method (called from the event handler)
private static void HandlePacket(Packet packet)
{
    switch (packet.Header.ToLower())
    {
        case "a data header here":
            // Do something with the packet data
            break;
    }
}
```

## Server
### Initialize
```cs
// Initialize the TcpServer somewhere
var tcpServer = new TcpServer(ServerIp, ServerPort);
tcpServer.HasPacket += TcpServer_HasPacket;
tcpServer.StartListener();
```

### Sending data
```cs
// Example method to build and send packets of data
public static void SendData(System.Net.Sockets.TcpClient client, string packetHeader, params string[] packetData)
{
    var packet = new Packet(packetHeader, packetData);
    tcpServer.SendDataTo(client, packet);
}
```

### Receiving data
```cs
// Example TcpServer_HasPacket event handler
private static void TcpServer_HasPacket(object sender, HasPacketEventArgs e)
{
    HandlePacket(e.Sender, e.Packet);
}

// Example HandlePacket method (called from the event handler)
private static void HandlePacket(System.Net.Sockets.TcpClient client, Packet packet)
{
    switch (packet.Header.ToLower())
    {
        case "a data header here":
            // Do something with the packet data
            break;
    }
}
```

# Security
## Setting a SecretKey
Within the `DataTransferProtocol` class, there is a member called `SecretKey` intended to secure trafic created by this library via encryption. It can easily be set by calling the `DataTransferProtocol.SetSecretKey()` method.

`SecretKey` is defined as an `internal static string` so that you may use external secure mechanisms to set the value. It could also be defined as an `internal const string` so that you can set it in code, rather than during run-time. However, unless your application is obfuscated (encrypting or hiding away constant values), this compromises the network security of your application. This approach also breaks the `DataTransferProtocol.SetSecretKey()` method, but it can just be deleted.

The client and server applications implementing this library must have the same `SecretKey` when communicating with each other or else, they will not be able to decrypt each other's traffic.

`SecretKey` can't be null or empty and it must be able to be converted from a base 64 `string` to a `byte[32]`.

## Generating a SecretKey
If you're looking for an easy way to get a secret key to use that's compatible with this library, consider the following method.

```cs
public static void GenerateUsableKey()
{
    var key = NewKey();
    var keyString = Convert.ToBase64String(key);

    // You want to use the value of `keyString` as your SecretKey
    Console.WriteLine($"Key As String: {keyString}");
}
```

This method is not included in the library but, it can be implemented into your copy of the library or, retooled to be used outside of the library in order to generate a secret key for your application.