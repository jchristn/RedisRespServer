# RedisRespServer

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

A .NET 8.0 library for building Redis-compatible servers using the RESP (Redis Serialization Protocol). Provides low-level protocol parsing and event-driven architecture for implementing custom Redis servers.

## Motivation

The motivation behind building this library is to 1) expose an interface for C# developers to build their own Redis-compatible backend services and 2) have a platform on which to rapidly prototype data path improvements, enhancements, integrations, or anything else they can dream without requiring a thorough understand of Redis and C.  The intent of the library is to inspire exploration, and in no uncertain terms, NOT to try and rebuild the wheel or otherwise create an alternative to a beloved, mature platform like Redis.

## Features

- 🚀 **RESP Protocol Support** - RESP2 and RESP3 protocol parsing
- 🔧 **Event-Driven Architecture** - Subscribe to specific RESP data types (arrays, bulk strings, etc.)
- 🏗️ **Modular Design** - Build custom Redis servers with `RespListener` and `RespInterface`
- ⚡ **Async/Await Support** - Non-blocking TCP server with proper async patterns
- 🧵 **Multi-Client Support** - Handle multiple concurrent client connections
- 📊 **Data Type Support** - String, Hash, List, Set, SortedSet, JSON, Stream value types
- 🔐 **Authentication Framework** - Built-in authentication event handling

## Testing and Validation

This library and the Redish.Server project specifically have been tested with the [redis-cli](), [StackExchange.Redis](), and [Redis Insight]().  Basic functionality and interoperability have been tested.  Please file an issue if you encounter any incompatibilities.

## Quick Start

### 1. Using Redish.Server (Pre-built Redis Server)

The fastest way to get a Redis-compatible server running:

```bash
# Clone and build
git clone https://github.com/jchristn/RedisRespServer.git
cd RedisRespServer/src
dotnet build

# Run the server
cd Redish.Server
dotnet run
```

Server starts on port 6379 and supports Redis commands like SET, GET, DEL, MSET, MGET, INCR, INCRBY, DECR, HMSET, and more.

### 2. Using Sample.RedisInterface (Basic Example)

```bash
cd Sample.RedisInterface
dotnet run
```

This starts a simple Redis server on port 6379 with basic command support.

## Core Architecture

The library has three main components:

### 1. RespListener (Low-level TCP + Protocol Parser)
- TCP server listening on configurable port
- Parses RESP2/RESP3 protocol messages
- Raises events for each RESP data type
- Handles client connections and disconnections

### 2. RespInterface (High-level API)
- Wraps RespListener with additional functionality
- Provides both event handlers and functional handlers
- Includes authentication support
- Manages client responses

### 3. Storage Layer (Application-specific)
- You implement your own storage logic
- Built-in `RedisValue` base class with expiration support
- Sample implementations show in-memory storage

## Building Your Own Redis Server

### Basic Server with RespListener

```csharp
using RedisResp;

var listener = new RespListener(6379);

// Handle array commands (like SET key value)
listener.ArrayReceived += (sender, e) => {
    if (e.Value is object[] args && args.Length >= 2)
    {
        string command = args[0].ToString().ToUpper();
        Console.WriteLine($"Received command: {command}");
        // Implement your command handling here
    }
};

// Handle client connections
listener.ClientConnected += (sender, e) => 
    Console.WriteLine($"Client connected: {e.GUID}");

await listener.StartAsync();
Console.WriteLine("Server running on port 6379");
Console.ReadKey();
```

### Using RespInterface for Enhanced Features

```csharp
using RedisResp;

var listener = new RespListener(6379);
var respInterface = new RespInterface(listener);

// Traditional event-driven approach
respInterface.ArrayReceived += HandleRedisCommand;

// Or functional approach  
respInterface.ArrayHandler = (e) => {
    // Handle command and return response string
    return "+OK\r\n";
};

await respInterface.StartAsync();
```

### Implementing Redis Commands

Based on the Sample.RedisInterface project, here's how to handle basic commands:

```csharp
private static readonly ConcurrentDictionary<string, RedisValue> _storage = new();

private async void HandleRedisCommand(object sender, RespDataReceivedEventArgs e)
{
    if (e.Value is not object[] args || args.Length == 0) return;
    
    string command = args[0].ToString().ToUpper();
    string response = command switch
    {
        "GET" when args.Length >= 2 => HandleGet(args[1].ToString()),
        "SET" when args.Length >= 3 => HandleSet(args[1].ToString(), args[2].ToString()),
        "DEL" when args.Length >= 2 => HandleDel(args[1].ToString()),
        "EXISTS" when args.Length >= 2 => HandleExists(args[1].ToString()),
        "KEYS" when args.Length >= 2 => HandleKeys(args[1].ToString()),
        "MSET" when args.Length >= 3 => HandleMset(args),
        "MGET" when args.Length >= 2 => HandleMget(args),
        "INCR" when args.Length >= 2 => HandleIncr(args[1].ToString()),
        "INCRBY" when args.Length >= 3 => HandleIncrBy(args[1].ToString(), args[2].ToString()),
        "DECR" when args.Length >= 2 => HandleDecr(args[1].ToString()),
        "HMSET" when args.Length >= 4 => HandleHmset(args),
        "PING" => "+PONG\r\n",
        "FLUSHDB" => HandleFlushDB(),
        _ => "-ERR unknown command\r\n"
    };
    
    // Send response back to client
    await SendResponseAsync(e.ClientGUID, response);
}

private string HandleGet(string key)
{
    if (_storage.TryGetValue(key, out var value) && !value.IsExpired && value is StringValue stringVal)
    {
        return $"${stringVal.Value.Length}\r\n{stringVal.Value}\r\n";
    }
    return "$-1\r\n"; // null
}

private string HandleSet(string key, string value)
{
    _storage[key] = new StringValue(value);
    return "+OK\r\n";
}
```

### Sending Responses to Clients

The Sample.RedisInterface shows how to send responses by writing directly to the client's NetworkStream:

```csharp
private async Task SendResponseAsync(Guid clientGuid, string response)
{
    if (_clientConnections.TryGetValue(clientGuid, out var client) && client.Connected)
    {
        var stream = client.GetStream();
        var data = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(data, 0, data.Length);
    }
}
```

## Custom Storage Backends

The library includes a `StorageBase` abstract class in Redish.Server for implementing custom storage:

```csharp
using Redish.Server.Storage;

public class CustomStorageDriver : StorageBase
{
    public override RedisValue this[string key] 
    { 
        get => GetFromYourStorage(key);
        set => SaveToYourStorage(key, value);
    }
    
    public override bool TryAdd(string key, RedisValue value)
    {
        // Implement atomic add operation
    }
    
    public override bool Remove(string key)
    {
        // Implement key removal
    }
    
    // Implement other abstract methods...
}
```

## Supported RESP Data Types

The `RespListener` can parse and raise events for:

### RESP2 Types
- **Simple Strings** (`+OK\r\n`) - `SimpleStringReceived` event
- **Errors** (`-ERR message\r\n`) - `ErrorReceived` event  
- **Integers** (`:100\r\n`) - `IntegerReceived` event
- **Bulk Strings** (`$5\r\nhello\r\n`) - `BulkStringReceived` event
- **Arrays** (`*2\r\n$3\r\nSET\r\n$3\r\nkey\r\n`) - `ArrayReceived` event
- **Null** (`$-1\r\n`) - `NullReceived` event

### RESP3 Types  
- **Doubles** (`,1.23\r\n`) - `DoubleReceived` event
- **Booleans** (`#t\r\n`, `#f\r\n`) - `BooleanReceived` event
- **Big Numbers** (`(123456789\r\n`) - `BigNumberReceived` event
- **Blob Errors** (`!5\r\nerror\r\n`) - `BlobErrorReceived` event
- **Verbatim Strings** (`=15\r\ntxt:hello world\r\n`) - `VerbatimStringReceived` event
- **Maps** (`%1\r\n+key\r\n+value\r\n`) - `MapReceived` event
- **Sets** (`~2\r\n+a\r\n+b\r\n`) - `SetReceived` event
- **Attributes** (`|1\r\n+attr\r\n+val\r\n`) - `AttributeReceived` event
- **Push** (`>2\r\n+pubsub\r\n+message\r\n`) - `PushReceived` event

## RedisValue Types

The library defines these value types in `RedisValueType` enum:

```csharp
public enum RedisValueType
{
    String,     // Basic string values
    Hash,       // Hash maps  
    List,       // Ordered lists
    Set,        // Unordered sets
    SortedSet,  // Scored sorted sets
    Json,       // JSON documents
    Stream      // Redis streams
}
```

Each `RedisValue` has automatic expiration support with `ExpiresAt`, `IsExpired`, and `SetExpiration()` methods.

## Sample Projects

The repository includes several working examples:

- **Redish.Server** - Full-featured Redis server with authentication and advanced data types
- **Sample.RedisInterface** - Basic Redis server using `RespInterface` 
- **Sample.RedisServer** - Alternative implementation example
- **Test.StackExchangeRedis** - Compatibility testing with StackExchange.Redis client

## Testing Your Server

Test with redis-cli:

```bash
redis-cli -h localhost -p 6379
127.0.0.1:6379> SET mykey "Hello"
OK
127.0.0.1:6379> GET mykey
"Hello"
127.0.0.1:6379> MSET key1 "value1" key2 "value2"
OK
127.0.0.1:6379> MGET key1 key2
1) "value1"
2) "value2"
127.0.0.1:6379> SET counter 10
OK  
127.0.0.1:6379> INCR counter
(integer) 11
127.0.0.1:6379> INCRBY counter 5
(integer) 16
127.0.0.1:6379> DECR counter
(integer) 15
127.0.0.1:6379> HMSET user name "John" age "30"
OK
127.0.0.1:6379> PING
PONG
```

Or run the test projects:

```bash
cd Test.StackExchangeRedis
dotnet run
```

## Build Requirements

- .NET 8.0 SDK
- No external dependencies for core library
- Redish.Server uses SerializationHelper and SyslogLogging packages

## Project Structure

```
src/
├── RedisRespServer/           # Core library (RespListener, RespInterface)
├── Redish.Server/            # Full Redis server implementation  
├── Sample.RedisInterface/    # Basic example server
├── Sample.RedisServer/       # Alternative example
├── Test.StackExchangeRedis/  # Compatibility tests
└── Test/                     # Additional test utilities
```

## License

This project is licensed under the MIT License - see the [LICENSE.md](../LICENSE.md) file for details.

---

**Build your own Redis-compatible server in minutes!** ⚡