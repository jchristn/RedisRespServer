namespace Sample.RedisInterface
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using RedisResp;

    /// <summary>
    /// A Redis-compatible server implementation using RespInterface with in-memory storage.
    /// </summary>
    /// <remarks>
    /// This class implements a subset of Redis commands using the RespInterface class
    /// for protocol handling and in-memory storage for data persistence. Supported commands
    /// include GET, SET, DEL, EXISTS, KEYS, PING, and FLUSHDB.
    /// </remarks>
    public partial class RedisInterfaceServer : IDisposable
    {
        private static readonly bool _DebugLogging = false;

        /// <summary>
        /// Gets the port number on which the server is listening.
        /// </summary>
        /// <value>The TCP port number.</value>
        public int Port { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the server is currently running.
        /// </summary>
        /// <value>true if the server is running; otherwise, false.</value>
        public bool IsRunning => _RespInterface?.Listener?.IsListening ?? false;

        /// <summary>
        /// Gets the current number of keys stored in the database.
        /// </summary>
        /// <value>The count of stored key-value pairs.</value>
        public int KeyCount => _Storage.Count(kvp => !kvp.Value.IsExpired);

        /// <summary>
        /// Gets the RespInterface instance used by this server.
        /// </summary>
        /// <value>The RespInterface providing event handling and authentication functionality.</value>
        public RespInterface RespInterface => _RespInterface;

        private readonly RespInterface _RespInterface;
        private readonly ConcurrentDictionary<string, RedisValue> _Storage = new();
        private readonly ConcurrentDictionary<Guid, TcpClient> _ClientConnections = new();
        private readonly ConcurrentDictionary<Guid, ClientInfo> _ClientInfos = new();
        private readonly DateTime _StartUtc = DateTime.UtcNow;
        private long _NextClientId = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisInterfaceServer"/> class.
        /// </summary>
        /// <param name="port">The port number to listen on. Defaults to 6381.</param>
        /// <remarks>
        /// Creates a new Redis server instance that will listen on the specified port.
        /// The server uses in-memory storage and supports a subset of Redis commands.
        /// Call <see cref="StartAsync"/> to begin accepting client connections.
        /// </remarks>
        public RedisInterfaceServer(int port = 6379)
        {
            Port = port;
            var listener = new RespListener(port);
            _RespInterface = new RespInterface(listener);

            // Subscribe to events using traditional event handlers
            _RespInterface.ArrayReceived += OnArrayReceived;
            _RespInterface.BulkStringReceived += OnBulkStringReceived;
            _RespInterface.ClientConnected += OnClientConnected;
            _RespInterface.ClientDisconnected += OnClientDisconnected;
            _RespInterface.ErrorOccurred += OnErrorOccurred;

            // Also demonstrate functional approach by setting up Action handlers
            _RespInterface.ClientConnectedAction = (e) => 
                Console.WriteLine($"[Action Handler] Client {e.GUID} connected from {e.RemoteEndPoint}");
            
            _RespInterface.ErrorAction = (e) => 
                Console.WriteLine($"[Action Handler] Error: {e.Message}" + (e.GUID.HasValue ? $" (Client: {e.GUID})" : ""));
        }

        /// <summary>
        /// Starts the Redis server and begins listening for client connections.
        /// </summary>
        /// <returns>A task representing the asynchronous start operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the server is already running.</exception>
        /// <exception cref="SocketException">Thrown if the port is already in use or other socket errors occur.</exception>
        /// <remarks>
        /// This method starts the underlying RESP listener and begins accepting client connections.
        /// The server will handle Redis commands asynchronously until <see cref="Stop"/> is called.
        /// </remarks>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Server is already running.");
            }

            Console.WriteLine($"Redis Interface Server starting on port {Port}...");
            await _RespInterface.Listener.StartAsync(cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"Redis Interface Server ready to accept connections on port {Port}");
        }

        /// <summary>
        /// Stops the Redis server and disconnects all clients.
        /// </summary>
        /// <remarks>
        /// This method stops the underlying RESP listener, disconnects all connected clients,
        /// and clears the client connection tracking. The in-memory storage is preserved.
        /// </remarks>
        public void Stop()
        {
            if (!IsRunning) return;

            Console.WriteLine("Shutting down Redis Interface Server...");
            _RespInterface.Listener.Stop();
            _ClientConnections.Clear();
            Console.WriteLine("Redis Interface Server stopped.");
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key to retrieve the value for.</param>
        /// <returns>The value associated with the key, or null if the key does not exist.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <remarks>
        /// This method implements the Redis GET command functionality.
        /// Returns null if the key does not exist in the storage.
        /// </remarks>
        public string? Get(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            if (_Storage.TryGetValue(key, out var redisValue) && !redisValue.IsExpired)
            {
                if (redisValue is StringValue stringValue)
                    return stringValue.Data;
            }
            
            return null;
        }

        /// <summary>
        /// Sets the value for the specified key.
        /// </summary>
        /// <param name="key">The key to set the value for.</param>
        /// <param name="value">The value to store.</param>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <remarks>
        /// This method implements the Redis SET command functionality.
        /// If the key already exists, its value will be overwritten.
        /// </remarks>
        public void Set(string key, string value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var stringValue = new StringValue(value);
            _Storage.AddOrUpdate(key, stringValue, (k, v) => stringValue);
        }

        /// <summary>
        /// Removes the specified key and its associated value from storage.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns>true if the key was found and removed; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <remarks>
        /// This method implements the Redis DEL command functionality.
        /// Returns false if the key does not exist.
        /// </remarks>
        public bool Delete(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            return _Storage.TryRemove(key, out _);
        }

        /// <summary>
        /// Checks if the specified key exists in storage.
        /// </summary>
        /// <param name="key">The key to check for existence.</param>
        /// <returns>true if the key exists; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <remarks>
        /// This method implements the Redis EXISTS command functionality.
        /// </remarks>
        public bool Exists(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            return _Storage.TryGetValue(key, out var value) && !value.IsExpired;
        }

        /// <summary>
        /// Gets all keys that match the specified pattern.
        /// </summary>
        /// <param name="pattern">The pattern to match keys against. Use "*" for all keys.</param>
        /// <returns>An array of keys that match the pattern.</returns>
        /// <exception cref="ArgumentNullException">Thrown when pattern is null.</exception>
        /// <remarks>
        /// This method implements the Redis KEYS command functionality.
        /// Currently supports simple "*" wildcard pattern matching.
        /// For performance reasons, avoid using this command in production with large datasets.
        /// </remarks>
        public string[] Keys(string pattern = "*")
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));

            if (pattern == "*")
            {
                return _Storage.Where(kvp => !kvp.Value.IsExpired).Select(kvp => kvp.Key).ToArray();
            }

            // Simple pattern matching - could be enhanced with proper glob pattern support
            return _Storage.Where(kvp => !kvp.Value.IsExpired && kvp.Key.Contains(pattern.Replace("*", "")))
                          .Select(kvp => kvp.Key).ToArray();
        }

        /// <summary>
        /// Removes all keys from the current database.
        /// </summary>
        /// <remarks>
        /// This method implements the Redis FLUSHDB command functionality.
        /// All stored key-value pairs will be permanently removed.
        /// </remarks>
        public void FlushDatabase()
        {
            _Storage.Clear();
        }

        /// <summary>
        /// Releases all resources used by the <see cref="RedisInterfaceServer"/>.
        /// </summary>
        /// <remarks>
        /// This method stops the server if it's running and disposes of the underlying RespInterface.
        /// After calling this method, the server instance cannot be reused.
        /// </remarks>
        public void Dispose()
        {
            Stop();
            _RespInterface?.Dispose();
        }

        private async void OnArrayReceived(object? sender, RespDataReceivedEventArgs e)
        {
            try
            {
                if (e.Value is not object[] arrayData || arrayData.Length == 0) return;

                // Convert array elements to strings and process as Redis command
                List<string> commandArgs = new List<string>();
                List<object?> rawElements = new List<object?>(); // Preserve raw data for binary commands
                
                foreach (var element in arrayData)
                {
                    rawElements.Add(element);
                    if (element != null)
                    {
                        commandArgs.Add(element.ToString()!);
                    }
                }

                if (commandArgs.Count > 0)
                {
                    Console.WriteLine($"[RespInterface] Received array command: {string.Join(" ", commandArgs)}");
                    if (_DebugLogging)
                    {
                        Console.WriteLine($"DEBUG OnArrayReceived: e.RawBytes = {(e.RawBytes == null ? "null" : $"byte[{e.RawBytes.Length}]")}");
                    }
                    await HandleRedisCommand(commandArgs.ToArray(), e.ClientGUID, rawElements?.Cast<object>().ToArray(), e.RawBytes, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling array command: {ex.Message}");
            }
        }

        private async void OnBulkStringReceived(object? sender, RespDataReceivedEventArgs e)
        {
            try
            {
                if (e.Value is not string command) return;

                Console.WriteLine($"[RespInterface] Received bulk string command: {command}");
                await HandleSimpleCommand(command, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling bulk string command: {ex.Message}");
            }
        }

        private void OnClientConnected(object? sender, ClientConnectedEventArgs e)
        {
            Console.WriteLine($"[RespInterface] Client connected: {e.GUID} from {e.RemoteEndPoint}");
            
            // Retrieve and store client connection for response sending
            var clientInfo = _RespInterface.Listener.RetrieveClientByGuid(e.GUID);
            if (clientInfo?.TcpClient != null)
            {
                _ClientConnections[e.GUID] = clientInfo.TcpClient;
                
                // Create client info entry
                int clientId = (int)System.Threading.Interlocked.Increment(ref _NextClientId);
                _ClientInfos[e.GUID] = new ClientInfo
                {
                    ClientId = clientId,
                    ConnectedAt = DateTime.UtcNow
                };
            }
        }

        private void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
        {
            if (_ClientInfos.TryGetValue(e.GUID, out var clientInfo))
            {
                TimeSpan duration = DateTime.UtcNow - clientInfo.ConnectedAt;
                Console.WriteLine($"[RespInterface] Client disconnected: {e.GUID} - {e.Reason} (connected for {duration.TotalSeconds:F2}s, lib: {clientInfo.LibraryName} {clientInfo.LibraryVersion})");
            }
            else
            {
                Console.WriteLine($"[RespInterface] Client disconnected: {e.GUID} - {e.Reason}");
            }
            
            _ClientConnections.TryRemove(e.GUID, out _);
            _ClientInfos.TryRemove(e.GUID, out _);
        }

        private void OnErrorOccurred(object? sender, ErrorEventArgs e)
        {
            string clientInfo = e.GUID.HasValue ? $" (Client: {e.GUID})" : "";
            Console.WriteLine($"[RespInterface] Error occurred: {e.Message}{clientInfo}");
            
            if (e.Exception != null)
            {
                Console.WriteLine($"Exception details: {e.Exception}");
            }
        }

        private async Task HandleSimpleCommand(string commandData, CancellationToken cancellationToken = default)
        {
            try
            {
                // Parse basic commands from the raw data
                // This is a simplified parser - real Redis protocol parsing is more complex
                string[] lines = commandData.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0) return;

                // Extract command and arguments
                List<string> commandParts = new List<string>();
                
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.StartsWith("*") || line.StartsWith("$")) continue;
                    if (line.StartsWith("+") || line.StartsWith("-") || line.StartsWith(":")) continue;
                    
                    commandParts.Add(line);
                }

                if (commandParts.Count == 0) return;

                string command = commandParts[0].ToUpperInvariant();
                
                switch (command)
                {
                    case "PING":
                        Console.WriteLine("Executed: PING -> PONG");
                        break;
                        
                    case "GET":
                        if (commandParts.Count > 1)
                        {
                            string? value = Get(commandParts[1]);
                            Console.WriteLine($"Executed: GET {commandParts[1]} -> {value ?? "(null)"}");
                        }
                        break;
                        
                    case "SET":
                        if (commandParts.Count > 2)
                        {
                            Set(commandParts[1], commandParts[2]);
                            Console.WriteLine($"Executed: SET {commandParts[1]} {commandParts[2]} -> OK");
                        }
                        break;
                        
                    case "DEL":
                        if (commandParts.Count > 1)
                        {
                            bool deleted = Delete(commandParts[1]);
                            Console.WriteLine($"Executed: DEL {commandParts[1]} -> {(deleted ? 1 : 0)}");
                        }
                        break;
                        
                    case "EXISTS":
                        if (commandParts.Count > 1)
                        {
                            bool exists = Exists(commandParts[1]);
                            Console.WriteLine($"Executed: EXISTS {commandParts[1]} -> {(exists ? 1 : 0)}");
                        }
                        break;
                        
                    case "KEYS":
                        string pattern = commandParts.Count > 1 ? commandParts[1] : "*";
                        string[] keys = Keys(pattern);
                        Console.WriteLine($"Executed: KEYS {pattern} -> [{string.Join(", ", keys)}]");
                        break;
                        
                    case "FLUSHDB":
                        FlushDatabase();
                        Console.WriteLine("Executed: FLUSHDB -> OK");
                        break;
                        
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing command: {ex.Message}");
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task HandleRedisCommand(
            string[] commandArgs, 
            Guid clientGuid, 
            object[]? rawElements = null, 
            byte[]? arrayRawBytes = null,
            CancellationToken cancellationToken = default)
        {
            if (commandArgs.Length == 0) return;

            string command = commandArgs[0].ToUpperInvariant();
            string response;

            try
            {
                switch (command)
                {
                    case "PING":
                        response = "+PONG\r\n";
                        Console.WriteLine("Executed: PING -> PONG");
                        break;

                    case "ECHO":
                        if (commandArgs.Length > 1)
                        {
                            string echoValue = commandArgs[1];
                            
                            // Convert the echo value to bytes using Latin1 to preserve binary data
                            byte[] echoBytes = System.Text.Encoding.Latin1.GetBytes(echoValue);
                            
                            // Build RESP bulk string response as raw bytes to avoid UTF-8 corruption
                            byte[] lengthPrefix = System.Text.Encoding.ASCII.GetBytes($"${echoBytes.Length}\r\n");
                            byte[] terminator = System.Text.Encoding.ASCII.GetBytes("\r\n");
                            
                            var respResponse = new byte[lengthPrefix.Length + echoBytes.Length + terminator.Length];
                            lengthPrefix.CopyTo(respResponse, 0);
                            echoBytes.CopyTo(respResponse, lengthPrefix.Length);
                            terminator.CopyTo(respResponse, lengthPrefix.Length + echoBytes.Length);
                            
                            await SendBinaryResponse(clientGuid, respResponse, cancellationToken).ConfigureAwait(false);
                            Console.WriteLine($"Executed: ECHO (binary preserved) -> {echoBytes.Length} bytes");
                            return; // Skip the normal response handling since we sent binary response
                        }
                        else
                        {
                            response = "-ERR wrong number of arguments for 'echo' command\r\n";
                        }
                        break;

                    case "GET":
                        if (commandArgs.Length > 1)
                        {
                            var value = Get(commandArgs[1]);
                            response = value != null ? $"${value.Length}\r\n{value}\r\n" : FormatNullResponse(clientGuid);
                            Console.WriteLine($"Executed: GET {commandArgs[1]} -> {value ?? "(null)"}");
                        }
                        else
                        {
                            response = "-ERR wrong number of arguments for 'get' command\r\n";
                        }
                        break;

                    case "SET":
                        if (commandArgs.Length > 2)
                        {
                            Set(commandArgs[1], commandArgs[2]);
                            response = "+OK\r\n";
                            Console.WriteLine($"Executed: SET {commandArgs[1]} {commandArgs[2]} -> OK");
                        }
                        else
                        {
                            response = "-ERR wrong number of arguments for 'set' command\r\n";
                        }
                        break;

                    case "DEL":
                        if (commandArgs.Length > 1)
                        {
                            var deleted = Delete(commandArgs[1]);
                            response = FormatBooleanResponse(deleted, clientGuid);
                            Console.WriteLine($"Executed: DEL {commandArgs[1]} -> {(deleted ? 1 : 0)}");
                        }
                        else
                        {
                            response = "-ERR wrong number of arguments for 'del' command\r\n";
                        }
                        break;

                    case "EXISTS":
                        if (commandArgs.Length > 1)
                        {
                            var exists = Exists(commandArgs[1]);
                            response = FormatBooleanResponse(exists, clientGuid);
                            Console.WriteLine($"Executed: EXISTS {commandArgs[1]} -> {(exists ? 1 : 0)}");
                        }
                        else
                        {
                            response = "-ERR wrong number of arguments for 'exists' command\r\n";
                        }
                        break;

                    case "KEYS":
                        var pattern = commandArgs.Length > 1 ? commandArgs[1] : "*";
                        string[] keys = Keys(pattern);
                        var keysArray = string.Join("", keys.Select(k => $"${k.Length}\r\n{k}\r\n"));
                        response = $"*{keys.Length}\r\n{keysArray}";
                        Console.WriteLine($"Executed: KEYS {pattern} -> [{string.Join(", ", keys)}]");
                        break;

                    case "FLUSHDB":
                        FlushDatabase();
                        response = "+OK\r\n";
                        Console.WriteLine("Executed: FLUSHDB -> OK");
                        break;

                    case "AUTH":
                        response = HandleAuthCommand(commandArgs);
                        break;

                    case "INFO":
                        response = HandleInfoCommand(commandArgs);
                        break;

                    case "CLIENT":
                        response = HandleClientCommand(commandArgs, clientGuid);
                        break;

                    case "CONFIG":
                        response = HandleConfigCommand(commandArgs);
                        break;

                    case "SENTINEL":
                        response = HandleSentinelCommand(commandArgs);
                        break;

                    case "CLUSTER":
                        response = HandleClusterCommand(commandArgs);
                        break;

                    case "SUBSCRIBE":
                        response = HandleSubscribeCommand(commandArgs, clientGuid);
                        break;

                    case "UNSUBSCRIBE":
                        response = HandleUnsubscribeCommand(commandArgs, clientGuid);
                        break;

                    case "PUBLISH":
                        response = HandlePublishCommand(commandArgs);
                        break;

                    case "HELLO":
                        response = HandleHelloCommand(commandArgs, clientGuid);
                        break;

                    case "COMMAND":
                        response = HandleCommandCommand(commandArgs);
                        break;

                    case "SELECT":
                        response = HandleSelectCommand(commandArgs);
                        break;

                    case "ROLE":
                        response = HandleRoleCommand(commandArgs);
                        break;

                    case "TIME":
                        response = HandleTimeCommand(commandArgs);
                        break;

                    case "MEMORY":
                        response = HandleMemoryCommand(commandArgs);
                        break;

                    case "ACL":
                        response = HandleAclCommand(commandArgs);
                        break;

                    case "MODULE":
                        response = HandleModuleCommand(commandArgs);
                        break;

                    case "LATENCY":
                        response = HandleLatencyCommand(commandArgs);
                        break;

                    case "SCAN":
                        var scanCursor = commandArgs.Length > 1 ? commandArgs[1] : "0";
                        var scanPattern = "*"; // default pattern
                        
                        // Parse MATCH parameter if present
                        for (int i = 2; i < commandArgs.Length - 1; i++)
                        {
                            if (commandArgs[i].ToUpperInvariant() == "MATCH")
                            {
                                scanPattern = commandArgs[i + 1];
                                break;
                            }
                        }
                        
                        var scanKeys = Keys(scanPattern);
                        var scanKeysArray = string.Join("", scanKeys.Select(k => $"${k.Length}\r\n{k}\r\n"));
                        
                        // SCAN returns: [new_cursor, [key1, key2, ...]]
                        // We'll always return cursor "0" to indicate iteration is complete
                        response = $"*2\r\n$1\r\n0\r\n*{scanKeys.Length}\r\n{scanKeysArray}";
                        Console.WriteLine($"Executed: SCAN {scanCursor} MATCH {scanPattern} -> [{string.Join(", ", scanKeys)}]");
                        break;

                    case "TYPE":
                        response = HandleTypeCommand(commandArgs);
                        break;

                    case "TTL":
                        response = HandleTtlCommand(commandArgs);
                        break;

                    case "MGET":
                        response = HandleMgetCommand(commandArgs, clientGuid);
                        break;

                    case "HGETALL":
                        response = HandleHgetallCommand(commandArgs, clientGuid);
                        break;

                    case "INCRBYFLOAT":
                        response = HandleIncrByFloatCommand(commandArgs, clientGuid);
                        break;

                    case "STRLEN":
                        response = HandleStrlenCommand(commandArgs);
                        break;

                    case "GETRANGE":
                        response = HandleGetrangeCommand(commandArgs);
                        break;

                    case "HSET":
                        response = HandleHsetCommand(commandArgs);
                        break;

                    case "HGET":
                        response = HandleHgetCommand(commandArgs);
                        break;

                    case "HDEL":
                        response = HandleHdelCommand(commandArgs);
                        break;

                    case "HLEN":
                        response = HandleHlenCommand(commandArgs);
                        break;

                    case "DBSIZE":
                        response = HandleDbsizeCommand(commandArgs);
                        break;

                    case "EXPIRE":
                        response = HandleExpireCommand(commandArgs);
                        break;

                    case "PERSIST":
                        response = HandlePersistCommand(commandArgs);
                        break;

                    case "HSCAN":
                        response = HandleHscanCommand(commandArgs);
                        break;

                    case "HEXISTS":
                        response = HandleHexistsCommand(commandArgs);
                        break;

                    // List commands
                    case "RPUSH":
                        response = HandleRpushCommand(commandArgs);
                        break;
                    case "LPUSH":
                        response = HandleLpushCommand(commandArgs);
                        break;
                    case "RPOP":
                        response = HandleRpopCommand(commandArgs);
                        break;
                    case "LPOP":
                        response = HandleLpopCommand(commandArgs);
                        break;
                    case "LRANGE":
                        response = HandleLrangeCommand(commandArgs);
                        break;
                    case "LLEN":
                        response = HandleLlenCommand(commandArgs);
                        break;

                    // Set commands
                    case "SADD":
                        response = HandleSaddCommand(commandArgs);
                        break;
                    case "SREM":
                        response = HandleSremCommand(commandArgs);
                        break;
                    case "SMEMBERS":
                        response = HandleSmembersCommand(commandArgs);
                        break;
                    case "SISMEMBER":
                        response = HandleSismemberCommand(commandArgs);
                        break;
                    case "SCARD":
                        response = HandleScardCommand(commandArgs);
                        break;
                    case "SPOP":
                        response = HandleSpopCommand(commandArgs);
                        break;
                    case "SRANDMEMBER":
                        response = HandleSrandmemberCommand(commandArgs);
                        break;

                    // Sorted Set commands
                    case "ZADD":
                        response = HandleZaddCommand(commandArgs);
                        break;
                    case "ZREM":
                        response = HandleZremCommand(commandArgs);
                        break;
                    case "ZSCORE":
                        response = HandleZscoreCommand(commandArgs);
                        break;
                    case "ZCARD":
                        response = HandleZcardCommand(commandArgs);
                        break;
                    case "ZRANGE":
                        response = HandleZrangeCommand(commandArgs);
                        break;
                    case "ZINCRBY":
                        response = HandleZincrbyCommand(commandArgs);
                        break;

                    // JSON commands
                    case "JSON.SET":
                        response = HandleJsonSetCommand(commandArgs);
                        break;
                    case "JSON.GET":
                        response = HandleJsonGetCommand(commandArgs);
                        break;
                    case "JSON.DEL":
                        response = HandleJsonDelCommand(commandArgs);
                        break;

                    // Stream commands
                    case "XADD":
                        response = HandleXaddCommand(commandArgs);
                        break;
                    case "XRANGE":
                        response = HandleXrangeCommand(commandArgs);
                        break;
                    case "XLEN":
                        response = HandleXlenCommand(commandArgs);
                        break;
                    case "XDEL":
                        response = HandleXdelCommand(commandArgs);
                        break;
                    case "XINFO":
                        response = HandleXinfoCommand(commandArgs);
                        break;

                    // String commands
                    case "MSET":
                        response = HandleMsetCommand(commandArgs);
                        break;
                    case "INCR":
                        response = HandleIncrCommand(commandArgs);
                        break;
                    case "DECR":
                        response = HandleDecrCommand(commandArgs);
                        break;
                    case "INCRBY":
                        response = HandleIncrByCommand(commandArgs);
                        break;
                    case "HMSET":
                        response = HandleHmsetCommand(commandArgs);
                        break;

                    default:
                        response = $"-ERR unknown command '{command}'\r\n";
                        Console.WriteLine($"Unknown command: {command}");
                        break;
                }

                await SendStringResponse(clientGuid, response, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing command: {ex.Message}");
                await SendStringResponse(clientGuid, "-ERR internal server error\r\n", cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task SendStringResponse(
            Guid clientGuid, 
            string response,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (_ClientConnections.TryGetValue(clientGuid, out var client) && client.Connected)
                {
                    var data = System.Text.Encoding.UTF8.GetBytes(response);
                    var stream = client.GetStream();
                    await stream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    
                    // Add small delay to prevent overwhelming the client
                    await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    Console.WriteLine($"Client {clientGuid} not found or disconnected");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending response to client {clientGuid}: {ex.Message}");
                // Remove disconnected client
                _ClientConnections.TryRemove(clientGuid, out _);
                _ClientInfos.TryRemove(clientGuid, out _);
            }
        }

        private RespVersionEnum GetClientRespVersion(Guid clientGuid)
        {
            if (_ClientInfos.TryGetValue(clientGuid, out var clientInfo))
            {
                return clientInfo.RespVersion;
            }
            return RespVersionEnum.RESP2; // Default to RESP2
        }

        private string FormatBooleanResponse(bool value, Guid clientGuid)
        {
            var respVersion = GetClientRespVersion(clientGuid);
            if (respVersion == RespVersionEnum.RESP3)
            {
                // RESP3 boolean format: # followed by t or f
                return value ? "#t\r\n" : "#f\r\n";
            }
            else
            {
                // RESP2 integer format: : followed by 1 or 0
                return value ? ":1\r\n" : ":0\r\n";
            }
        }

        private string FormatNullResponse(Guid clientGuid)
        {
            var respVersion = GetClientRespVersion(clientGuid);
            if (respVersion == RespVersionEnum.RESP3)
            {
                // RESP3 null format
                return "_\r\n";
            }
            else
            {
                // RESP2 null bulk string format
                return "$-1\r\n";
            }
        }

        private string FormatDoubleResponse(double value, Guid clientGuid)
        {
            var respVersion = GetClientRespVersion(clientGuid);
            if (respVersion == RespVersionEnum.RESP3)
            {
                // RESP3 double format: , followed by the double value
                return $",{value:G17}\r\n";
            }
            else
            {
                // RESP2 bulk string format
                var valueStr = value.ToString("G17", CultureInfo.InvariantCulture);
                return $"${valueStr.Length}\r\n{valueStr}\r\n";
            }
        }

        private async Task SendBinaryResponse(
            Guid clientGuid, 
            byte[] binaryData,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (_ClientConnections.TryGetValue(clientGuid, out var client) && client.Connected)
                {
                    var stream = client.GetStream();
                    await stream.WriteAsync(binaryData, 0, binaryData.Length, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    
                    // Add small delay to prevent overwhelming the client
                    await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    Console.WriteLine($"Client {clientGuid} not found or disconnected");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending binary response to client {clientGuid}: {ex.Message}");
                // Remove disconnected client
                _ClientConnections.TryRemove(clientGuid, out _);
                _ClientInfos.TryRemove(clientGuid, out _);
            }
        }

        private string HandleAuthCommand(string[] commandArgs)
        {
            // Redis AUTH command can have 1 or 2 arguments:
            // AUTH password (for default user)
            // AUTH username password (for specific user)
            
            if (commandArgs.Length == 2)
            {
                // AUTH password (default user authentication)
                var password = commandArgs[1];
                
                // Use RespInterface.Authenticate if set, otherwise allow by default
                if (_RespInterface.Authenticate != null)
                {
                    bool authResult = _RespInterface.Authenticate(null, password);
                    if (authResult)
                    {
                        Console.WriteLine($"Executed: AUTH [password] -> OK (authenticated)");
                        return "+OK\r\n";
                    }
                    else
                    {
                        Console.WriteLine($"Executed: AUTH [password] -> WRONGPASS (authentication failed)");
                        return "-WRONGPASS invalid username-password pair\r\n";
                    }
                }
                else
                {
                    Console.WriteLine($"Executed: AUTH [password] -> OK (no authentication required)");
                    return "+OK\r\n";
                }
            }
            else if (commandArgs.Length == 3)
            {
                // AUTH username password
                var username = commandArgs[1];
                var password = commandArgs[2];
                
                // Use RespInterface.Authenticate if set, otherwise allow by default
                if (_RespInterface.Authenticate != null)
                {
                    bool authResult = _RespInterface.Authenticate(username, password);
                    if (authResult)
                    {
                        Console.WriteLine($"Executed: AUTH {username} [password] -> OK (authenticated)");
                        return "+OK\r\n";
                    }
                    else
                    {
                        Console.WriteLine($"Executed: AUTH {username} [password] -> WRONGPASS (authentication failed)");
                        return "-WRONGPASS invalid username-password pair\r\n";
                    }
                }
                else
                {
                    Console.WriteLine($"Executed: AUTH {username} [password] -> OK (no authentication required)");
                    return "+OK\r\n";
                }
            }
            else
            {
                return "-ERR wrong number of arguments for 'auth' command\r\n";
            }
        }

        private string HandleInfoCommand(string[] commandArgs)
        {
            var section = commandArgs.Length > 1 ? commandArgs[1].ToLowerInvariant() : "default";
            
            var uptime = (DateTime.UtcNow - _StartUtc).TotalSeconds;
            var info = new StringBuilder();
            
            switch (section)
            {
                case "server":
                    info.Append("# Server\r\n");
                    info.Append("redis_version:7.0.0\r\n");
                    info.Append("redis_git_sha1:00000000\r\n");
                    info.Append("redis_git_dirty:0\r\n");
                    info.Append("redis_build_id:0\r\n");
                    info.Append("redis_mode:standalone\r\n");
                    info.Append("os:Windows\r\n");
                    info.Append("arch_bits:64\r\n");
                    info.Append($"multiplexing_api:select\r\n");
                    info.Append($"process_id:{Environment.ProcessId}\r\n");
                    info.Append($"tcp_port:{Port}\r\n");
                    info.Append($"uptime_in_seconds:{(int)uptime}\r\n");
                    info.Append($"uptime_in_days:{(int)(uptime / 86400)}\r\n");
                    break;
                    
                case "replication":
                    info.Append("# Replication\r\n");
                    info.Append("role:master\r\n");
                    info.Append("connected_slaves:0\r\n");
                    info.Append("master_repl_offset:0\r\n");
                    info.Append("repl_backlog_active:0\r\n");
                    info.Append("repl_backlog_size:1048576\r\n");
                    break;
                    
                default:
                    info.Append("# Server\r\n");
                    info.Append("redis_version:7.0.0\r\n");
                    info.Append("redis_git_sha1:00000000\r\n");
                    info.Append("redis_git_dirty:0\r\n");
                    info.Append("redis_build_id:0\r\n");
                    info.Append("redis_mode:standalone\r\n");
                    info.Append("os:Windows\r\n");
                    info.Append("arch_bits:64\r\n");
                    info.Append($"process_id:{Environment.ProcessId}\r\n");
                    info.Append($"tcp_port:{Port}\r\n");
                    info.Append($"uptime_in_seconds:{(int)uptime}\r\n");
                    info.Append("\r\n");
                    info.Append("# Replication\r\n");
                    info.Append("role:master\r\n");
                    info.Append("connected_slaves:0\r\n");
                    info.Append("master_repl_offset:0\r\n");
                    break;
            }
            
            var infoString = info.ToString().TrimEnd('\r', '\n');
            var infoBytes = System.Text.Encoding.UTF8.GetBytes(infoString);
            Console.WriteLine($"Executed: INFO {section} -> {infoBytes.Length} bytes");
            return $"${infoBytes.Length}\r\n{infoString}\r\n";
        }

        private string HandleClientCommand(string[] commandArgs, Guid clientGuid)
        {
            if (commandArgs.Length < 2)
            {
                return "-ERR wrong number of arguments for 'client' command\r\n";
            }

            var subCommand = commandArgs[1].ToUpperInvariant();
            switch (subCommand)
            {
                case "SETNAME":
                    if (commandArgs.Length < 3)
                    {
                        return "-ERR wrong number of arguments for 'client setname' command\r\n";
                    }
                    
                    if (_ClientInfos.TryGetValue(clientGuid, out var clientInfo))
                    {
                        clientInfo.Name = commandArgs[2];
                        
                        // Also set the name in the RespListener's ClientInfo if available
                        var respClientInfo = _RespInterface.Listener.RetrieveClientByGuid(clientGuid);
                        if (respClientInfo != null)
                        {
                            respClientInfo.Name = commandArgs[2];
                        }
                        
                        Console.WriteLine($"Executed: CLIENT SETNAME {commandArgs[2]} -> OK");
                        return "+OK\r\n";
                    }
                    return "-ERR client not found\r\n";

                case "SETINFO":
                    if (commandArgs.Length < 4)
                    {
                        return "-ERR wrong number of arguments for 'client setinfo' command\r\n";
                    }
                    
                    if (_ClientInfos.TryGetValue(clientGuid, out var info))
                    {
                        var infoType = commandArgs[2].ToLowerInvariant();
                        var infoValue = commandArgs[3];
                        
                        switch (infoType)
                        {
                            case "lib-name":
                                info.LibraryName = infoValue;
                                break;
                            case "lib-ver":
                                info.LibraryVersion = infoValue;
                                break;
                        }
                        
                        Console.WriteLine($"Executed: CLIENT SETINFO {commandArgs[2]} {commandArgs[3]} -> OK");
                        return "+OK\r\n";
                    }
                    return "-ERR client not found\r\n";

                case "ID":
                    if (_ClientInfos.TryGetValue(clientGuid, out var idInfo))
                    {
                        Console.WriteLine($"Executed: CLIENT ID -> {idInfo.ClientId}");
                        return $":{idInfo.ClientId}\r\n";
                    }
                    return "-ERR client not found\r\n";

                default:
                    Console.WriteLine($"Unknown CLIENT subcommand: {subCommand}");
                    return $"-ERR unknown subcommand '{subCommand}'\r\n";
            }
        }

        private string HandleConfigCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
            {
                return "-ERR wrong number of arguments for 'config' command\r\n";
            }

            var subCommand = commandArgs[1].ToUpperInvariant();
            switch (subCommand)
            {
                case "GET":
                    if (commandArgs.Length < 3)
                    {
                        return "-ERR wrong number of arguments for 'config get' command\r\n";
                    }
                    
                    var parameter = commandArgs[2].ToLowerInvariant();
                    switch (parameter)
                    {
                        case "slave-read-only":
                            Console.WriteLine("Executed: CONFIG GET slave-read-only -> no");
                            return "*2\r\n$15\r\nslave-read-only\r\n$2\r\nno\r\n";
                            
                        case "databases":
                            Console.WriteLine("Executed: CONFIG GET databases -> 16");
                            return "*2\r\n$9\r\ndatabases\r\n$2\r\n16\r\n";
                            
                        default:
                            Console.WriteLine($"Executed: CONFIG GET {parameter} -> (empty)");
                            return "*0\r\n";
                    }

                default:
                    return $"-ERR unknown subcommand '{subCommand}'\r\n";
            }
        }

        private string HandleSentinelCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
            {
                return "-ERR wrong number of arguments for 'sentinel' command\r\n";
            }

            var subCommand = commandArgs[1].ToUpperInvariant();
            switch (subCommand)
            {
                case "MASTERS":
                    // Return proper SENTINEL MASTERS format - array of master info arrays  
                    // Each master is an array of field-value pairs
                    // For compatibility, return one mock master entry
                    Console.WriteLine("Executed: SENTINEL MASTERS -> mock master data");
                    return "*1\r\n" + // 1 master
                           "*30\r\n" + // 15 field-value pairs (30 elements total)
                           "$4\r\nname\r\n$9\r\nmymaster\r\n" +
                           "$2\r\nip\r\n$9\r\n127.0.0.1\r\n" +
                           "$4\r\nport\r\n$4\r\n6379\r\n" +
                           "$5\r\nrunid\r\n$32\r\n" + System.Guid.NewGuid().ToString().Replace("-", "") + "\r\n" +
                           "$5\r\nflags\r\n$6\r\nmaster\r\n" +
                           "$16\r\nlink-pending-commands\r\n$1\r\n0\r\n" +
                           "$9\r\nlink-refcount\r\n$1\r\n1\r\n" +
                           "$13\r\nlast-ping-sent\r\n$1\r\n0\r\n" +
                           "$14\r\nlast-ok-ping-reply\r\n$3\r\n123\r\n" +
                           "$13\r\nlast-ping-reply\r\n$3\r\n123\r\n" +
                           "$23\r\ndown-after-milliseconds\r\n$5\r\n30000\r\n" +
                           "$11\r\ninfo-refresh\r\n$10\r\n" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() + "\r\n" +
                           "$6\r\nrole-reported\r\n$6\r\nmaster\r\n" +
                           "$18\r\nrole-reported-time\r\n$10\r\n" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() + "\r\n" +
                           "$11\r\nconfig-epoch\r\n$1\r\n0\r\n" +
                           "$10\r\nnum-slaves\r\n$1\r\n0\r\n";
                    
                default:
                    return $"-ERR unknown subcommand '{subCommand}'\r\n";
            }
        }

        private string HandleClusterCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
            {
                return "-ERR wrong number of arguments for 'cluster' command\r\n";
            }

            var subCommand = commandArgs[1].ToUpperInvariant();
            switch (subCommand)
            {
                case "NODES":
                    // Return error indicating cluster not enabled
                    Console.WriteLine("Executed: CLUSTER NODES -> cluster not enabled");
                    return "-ERR This instance has cluster support disabled\r\n";
                    
                default:
                    return $"-ERR unknown subcommand '{subCommand}'\r\n";
            }
        }

        private string HandleSubscribeCommand(string[] commandArgs, Guid clientGuid)
        {
            if (commandArgs.Length < 2)
            {
                return "-ERR wrong number of arguments for 'subscribe' command\r\n";
            }

            var channel = commandArgs[1];
            
            // For StackExchange.Redis, we just need to acknowledge the subscription
            // Real pub/sub functionality would require more complex state management
            Console.WriteLine($"Executed: SUBSCRIBE {channel} -> OK (client: {clientGuid})");
            
            // Return subscription confirmation in Redis pub/sub format
            return $"*3\r\n$9\r\nsubscribe\r\n${channel.Length}\r\n{channel}\r\n:1\r\n";
        }

        private string HandleUnsubscribeCommand(string[] commandArgs, Guid clientGuid)
        {
            var channel = commandArgs.Length > 1 ? commandArgs[1] : "*";
            
            Console.WriteLine($"Executed: UNSUBSCRIBE {channel} -> OK (client: {clientGuid})");
            
            // Return unsubscription confirmation in Redis pub/sub format
            return $"*3\r\n$11\r\nunsubscribe\r\n${channel.Length}\r\n{channel}\r\n:0\r\n";
        }

        private string HandlePublishCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 3)
            {
                return "-ERR wrong number of arguments for 'publish' command\r\n";
            }

            var channel = commandArgs[1];
            var message = commandArgs[2];
            
            // For basic compatibility, just return 0 subscribers
            Console.WriteLine($"Executed: PUBLISH {channel} -> 0 subscribers");
            return ":0\r\n";
        }

        private string HandleHelloCommand(string[] commandArgs, Guid clientGuid)
        {
            // HELLO [protover [AUTH username password] [SETNAME clientname]]
            // For basic compatibility, support HELLO without authentication
            
            int protocolVersion = 2; // Default to RESP2
            
            if (commandArgs.Length > 1 && int.TryParse(commandArgs[1], out int requestedVersion))
            {
                if (requestedVersion == 3)
                {
                    protocolVersion = 3;
                }
                else if (requestedVersion != 2)
                {
                    return "-NOPROTO unsupported protocol version\r\n";
                }
            }

            // Update client info with negotiated RESP version
            if (_ClientInfos.TryGetValue(clientGuid, out var clientInfo))
            {
                clientInfo.RespVersion = (RespVersionEnum)protocolVersion;
                Console.WriteLine($"Client {clientGuid} negotiated RESP{protocolVersion}");
            }

            Console.WriteLine($"Executed: HELLO -> RESP{protocolVersion} (simplified response)");
            
            // Return response format based on negotiated protocol version
            if (protocolVersion == 3)
            {
                // RESP3 format using Map type (% prefix)
                var response = new StringBuilder();
                response.Append("%7\r\n"); // 7 key-value pairs in map format
                response.Append("$6\r\nserver\r\n$5\r\nredis\r\n");
                response.Append("$7\r\nversion\r\n$5\r\n7.0.0\r\n");
                response.Append("$5\r\nproto\r\n:").Append(protocolVersion).Append("\r\n");
                response.Append("$2\r\nid\r\n:1\r\n");
                response.Append("$4\r\nmode\r\n$10\r\nstandalone\r\n");
                response.Append("$4\r\nrole\r\n$6\r\nmaster\r\n");
                response.Append("$7\r\nmodules\r\n*0\r\n");
                return response.ToString();
            }
            else
            {
                // RESP2 format using Array type (* prefix) - existing format
                var response = new StringBuilder();
                response.Append("*14\r\n"); // 7 key-value pairs
                response.Append("$6\r\nserver\r\n$5\r\nredis\r\n");
                response.Append("$7\r\nversion\r\n$5\r\n7.0.0\r\n");
                response.Append("$5\r\nproto\r\n:").Append(protocolVersion).Append("\r\n");
                response.Append("$2\r\nid\r\n:1\r\n");
                response.Append("$4\r\nmode\r\n$10\r\nstandalone\r\n");
                response.Append("$4\r\nrole\r\n$6\r\nmaster\r\n");
                response.Append("$7\r\nmodules\r\n*0\r\n");
                return response.ToString();
            }
        }

        private string HandleCommandCommand(string[] commandArgs)
        {
            // Basic COMMAND implementation - return empty array for compatibility
            Console.WriteLine("Executed: COMMAND -> (empty list for compatibility)");
            return "*0\r\n";
        }

        private string HandleSelectCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
            {
                return "-ERR wrong number of arguments for 'select' command\r\n";
            }

            if (!int.TryParse(commandArgs[1], out int databaseIndex) || databaseIndex < 0)
            {
                return "-ERR invalid DB index\r\n";
            }

            // For basic compatibility, only support database 0
            if (databaseIndex == 0)
            {
                Console.WriteLine($"Executed: SELECT {databaseIndex} -> OK");
                return "+OK\r\n";
            }
            else
            {
                Console.WriteLine($"Executed: SELECT {databaseIndex} -> ERR (only database 0 supported)");
                return "-ERR DB index is out of range\r\n";
            }
        }

        private string HandleRoleCommand(string[] commandArgs)
        {
            Console.WriteLine("Executed: ROLE -> master");
            // Return master role with replication info
            return "*3\r\n$6\r\nmaster\r\n:0\r\n*0\r\n";
        }

        private string HandleTimeCommand(string[] commandArgs)
        {
            var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var microseconds = DateTimeOffset.UtcNow.Microsecond;
            Console.WriteLine($"Executed: TIME -> {unixTime}");
            
            return $"*2\r\n${unixTime.ToString().Length}\r\n{unixTime}\r\n${microseconds.ToString().Length}\r\n{microseconds}\r\n";
        }

        private string HandleMemoryCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
            {
                return "-ERR wrong number of arguments for 'memory' command\r\n";
            }

            var subCommand = commandArgs[1].ToUpperInvariant();
            switch (subCommand)
            {
                case "USAGE":
                    Console.WriteLine("Executed: MEMORY USAGE -> 1024");
                    return ":1024\r\n";
                    
                default:
                    return $"-ERR unknown subcommand '{subCommand}'\r\n";
            }
        }

        private string HandleAclCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
            {
                return "-ERR wrong number of arguments for 'acl' command\r\n";
            }

            var subCommand = commandArgs[1].ToUpperInvariant();
            switch (subCommand)
            {
                case "LIST":
                    Console.WriteLine("Executed: ACL LIST -> default user");
                    return "*1\r\n$12\r\nuser default\r\n";
                    
                case "WHOAMI":
                    Console.WriteLine("Executed: ACL WHOAMI -> default");
                    return "$7\r\ndefault\r\n";
                    
                case "USERS":
                    Console.WriteLine("Executed: ACL USERS -> default");
                    return "*1\r\n$7\r\ndefault\r\n";
                    
                default:
                    return $"-ERR unknown subcommand '{subCommand}'\r\n";
            }
        }

        private string HandleModuleCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
            {
                return "-ERR wrong number of arguments for 'module' command\r\n";
            }

            var subCommand = commandArgs[1].ToUpperInvariant();
            switch (subCommand)
            {
                case "LIST":
                    Console.WriteLine("Executed: MODULE LIST -> (empty)");
                    return "*0\r\n";
                    
                default:
                    return $"-ERR unknown subcommand '{subCommand}'\r\n";
            }
        }

        private string HandleLatencyCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
            {
                return "-ERR wrong number of arguments for 'latency' command\r\n";
            }

            var subCommand = commandArgs[1].ToUpperInvariant();
            switch (subCommand)
            {
                case "LATEST":
                    Console.WriteLine("Executed: LATENCY LATEST -> (empty)");
                    return "*0\r\n";
                    
                case "HISTORY":
                    Console.WriteLine("Executed: LATENCY HISTORY -> (empty)");
                    return "*0\r\n";
                    
                default:
                    return $"-ERR unknown subcommand '{subCommand}'\r\n";
            }
        }

        private string HandleTypeCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
            {
                return "-ERR wrong number of arguments for 'type' command\r\n";
            }

            var key = commandArgs[1];
            
            // Check if key exists and get its type
            if (_Storage.TryGetValue(key, out var redisValue) && !redisValue.IsExpired)
            {
                var typeName = redisValue.Type switch
                {
                    RedisValueType.String => "string",
                    RedisValueType.Hash => "hash",
                    RedisValueType.List => "list",
                    RedisValueType.Set => "set",
                    RedisValueType.SortedSet => "zset",
                    RedisValueType.Json => "ReJSON-RL",
                    RedisValueType.Stream => "stream",
                    _ => "string"
                };
                
                Console.WriteLine($"Executed: TYPE {key} -> {typeName}");
                return $"+{typeName}\r\n";
            }

            Console.WriteLine($"Executed: TYPE {key} -> none");
            return "+none\r\n";
        }

        private string HandleTtlCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
            {
                return "-ERR wrong number of arguments for 'ttl' command\r\n";
            }

            var key = commandArgs[1];
            
            // Check if key exists and get its TTL
            if (_Storage.TryGetValue(key, out var redisValue))
            {
                if (redisValue.IsExpired)
                {
                    Console.WriteLine($"Executed: TTL {key} -> -2 (key does not exist)");
                    return ":-2\r\n";
                }
                
                var ttl = redisValue.GetTtl();
                Console.WriteLine($"Executed: TTL {key} -> {ttl} seconds");
                return $":{ttl}\r\n";
            }
            
            Console.WriteLine($"Executed: TTL {key} -> -2 (key does not exist)");
            return ":-2\r\n";
        }

        private string HandleMgetCommand(string[] commandArgs, Guid clientGuid)
        {
            if (commandArgs.Length < 2)
            {
                return "-ERR wrong number of arguments for 'mget' command\r\n";
            }

            var respVersion = GetClientRespVersion(clientGuid);
            var values = new List<string?>();
            
            // Get values for all keys
            for (int i = 1; i < commandArgs.Length; i++)
            {
                values.Add(Get(commandArgs[i]));
            }

            // Format response based on RESP version
            var response = new StringBuilder();
            if (respVersion == RespVersionEnum.RESP3)
            {
                // RESP3 array format (same as RESP2 but can contain nulls properly)
                response.Append($"*{values.Count}\r\n");
                foreach (var value in values)
                {
                    if (value == null)
                    {
                        response.Append("_\r\n"); // RESP3 null
                    }
                    else
                    {
                        response.Append($"${value.Length}\r\n{value}\r\n");
                    }
                }
            }
            else
            {
                // RESP2 array format
                response.Append($"*{values.Count}\r\n");
                foreach (var value in values)
                {
                    if (value == null)
                    {
                        response.Append("$-1\r\n"); // RESP2 null bulk string
                    }
                    else
                    {
                        response.Append($"${value.Length}\r\n{value}\r\n");
                    }
                }
            }

            var keys = string.Join(" ", commandArgs.Skip(1));
            Console.WriteLine($"Executed: MGET {keys} -> {values.Count} values");
            return response.ToString();
        }

        private string HandleHgetallCommand(string[] commandArgs, Guid clientGuid)
        {
            if (commandArgs.Length != 2)
            {
                return "-ERR wrong number of arguments for 'hgetall' command\r\n";
            }

            var key = commandArgs[1];
            var respVersion = GetClientRespVersion(clientGuid);
            var response = new StringBuilder();
            
            if (_Storage.TryGetValue(key, out var redisValue) && !redisValue.IsExpired)
            {
                if (redisValue is HashValue hashValue)
                {
                    var allFields = hashValue.GetAll();
                    
                    if (respVersion == RespVersionEnum.RESP3)
                    {
                        // RESP3 Map format (% prefix)
                        response.Append($"%{allFields.Length / 2}\r\n");
                        for (int i = 0; i < allFields.Length; i += 2)
                        {
                            response.Append($"${allFields[i].Length}\r\n{allFields[i]}\r\n");
                            response.Append($"${allFields[i + 1].Length}\r\n{allFields[i + 1]}\r\n");
                        }
                    }
                    else
                    {
                        // RESP2 Array format (flat array of field-value pairs)
                        response.Append($"*{allFields.Length}\r\n");
                        foreach (var item in allFields)
                        {
                            response.Append($"${item.Length}\r\n{item}\r\n");
                        }
                    }

                    Console.WriteLine($"Executed: HGETALL {key} -> {allFields.Length / 2} fields (RESP{(int)respVersion})");
                    return response.ToString();
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }

            // Empty array/map for non-existent key
            if (respVersion == RespVersionEnum.RESP3)
            {
                Console.WriteLine($"Executed: HGETALL {key} -> 0 fields (key not found, RESP3)");
                return "%0\r\n";
            }
            else
            {
                Console.WriteLine($"Executed: HGETALL {key} -> 0 fields (key not found, RESP2)");
                return "*0\r\n";
            }
        }

        private string HandleIncrByFloatCommand(string[] commandArgs, Guid clientGuid)
        {
            if (commandArgs.Length != 3)
            {
                return "-ERR wrong number of arguments for 'incrbyfloat' command\r\n";
            }

            var key = commandArgs[1];
            if (!double.TryParse(commandArgs[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double increment))
            {
                return "-ERR value is not a valid float\r\n";
            }

            // Get current value or default to 0
            var currentValueStr = Get(key);
            double currentValue = 0;
            if (currentValueStr != null && !double.TryParse(currentValueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out currentValue))
            {
                return "-ERR value is not a valid float\r\n";
            }

            // Increment and store
            var newValue = currentValue + increment;
            var newValueStr = newValue.ToString("G17", CultureInfo.InvariantCulture);
            Set(key, newValueStr);

            // Format response based on RESP version
            var response = FormatDoubleResponse(newValue, clientGuid);
            Console.WriteLine($"Executed: INCRBYFLOAT {key} {increment} -> {newValue} (RESP{(int)GetClientRespVersion(clientGuid)})");
            return response;
        }

        private string HandleStrlenCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
            {
                return "-ERR wrong number of arguments for 'strlen' command\r\n";
            }

            var key = commandArgs[1];
            var value = Get(key);
            var length = value?.Length ?? 0;

            Console.WriteLine($"Executed: STRLEN {key} -> {length}");
            return $":{length}\r\n";
        }

        private string HandleGetrangeCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 4)
            {
                return "-ERR wrong number of arguments for 'getrange' command\r\n";
            }

            var key = commandArgs[1];
            if (!int.TryParse(commandArgs[2], out int start) || !int.TryParse(commandArgs[3], out int end))
            {
                return "-ERR value is not an integer or out of range\r\n";
            }

            var value = Get(key) ?? "";
            var result = GetRange(value, start, end);

            Console.WriteLine($"Executed: GETRANGE {key} {start} {end} -> \"{result}\"");
            return $"${result.Length}\r\n{result}\r\n";
        }

        private static string GetRange(string value, int start, int end)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            int len = value.Length;
            
            // Handle negative indices
            if (start < 0) start = len + start;
            if (end < 0) end = len + end;
            
            // Clamp to valid range
            start = Math.Max(0, Math.Min(start, len - 1));
            end = Math.Max(0, Math.Min(end, len - 1));
            
            // If start > end, return empty string
            if (start > end)
                return "";
                
            return value.Substring(start, end - start + 1);
        }

        private string HandleHsetCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 4 || commandArgs.Length % 2 != 0)
            {
                return "-ERR wrong number of arguments for 'hset' command\r\n";
            }

            var key = commandArgs[1];
            int fieldsAdded = 0;

            // Get or create hash
            HashValue hashValue;
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is HashValue existing)
                {
                    hashValue = existing;
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                hashValue = new HashValue();
                _Storage.AddOrUpdate(key, hashValue, (k, v) => hashValue);
            }

            // Set field-value pairs
            for (int i = 2; i < commandArgs.Length; i += 2)
            {
                var field = commandArgs[i];
                var value = commandArgs[i + 1];
                
                bool wasNewField = !hashValue.Fields.ContainsKey(field);
                hashValue.SetField(field, value);
                if (wasNewField) fieldsAdded++;
            }

            Console.WriteLine($"Executed: HSET {key} -> {fieldsAdded} fields added");
            return $":{fieldsAdded}\r\n";
        }

        private string HandleHgetCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 3)
            {
                return "-ERR wrong number of arguments for 'hget' command\r\n";
            }

            var key = commandArgs[1];
            var field = commandArgs[2];

            if (_Storage.TryGetValue(key, out var redisValue) && !redisValue.IsExpired)
            {
                if (redisValue is HashValue hashValue)
                {
                    var fieldValue = hashValue.GetField(field);
                    if (fieldValue != null)
                    {
                        Console.WriteLine($"Executed: HGET {key} {field} -> {fieldValue}");
                        return $"${fieldValue.Length}\r\n{fieldValue}\r\n";
                    }
                    else
                    {
                        Console.WriteLine($"Executed: HGET {key} {field} -> (null)");
                        return "$-1\r\n";
                    }
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }

            Console.WriteLine($"Executed: HGET {key} {field} -> (null) - key not found");
            return "$-1\r\n";
        }

        private string HandleHdelCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 3)
            {
                return "-ERR wrong number of arguments for 'hdel' command\r\n";
            }

            var key = commandArgs[1];
            int fieldsRemoved = 0;

            if (_Storage.TryGetValue(key, out var redisValue) && !redisValue.IsExpired)
            {
                if (redisValue is HashValue hashValue)
                {
                    // Remove each specified field
                    for (int i = 2; i < commandArgs.Length; i++)
                    {
                        var field = commandArgs[i];
                        if (hashValue.RemoveField(field))
                        {
                            fieldsRemoved++;
                        }
                    }

                    // If hash becomes empty, remove the key
                    if (hashValue.FieldCount == 0)
                    {
                        _Storage.TryRemove(key, out _);
                    }
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }

            Console.WriteLine($"Executed: HDEL {key} -> {fieldsRemoved} fields removed");
            return $":{fieldsRemoved}\r\n";
        }

        private string HandleHlenCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
            {
                return "-ERR wrong number of arguments for 'hlen' command\r\n";
            }

            var key = commandArgs[1];

            if (_Storage.TryGetValue(key, out var redisValue) && !redisValue.IsExpired)
            {
                if (redisValue is HashValue hashValue)
                {
                    var count = hashValue.FieldCount;
                    Console.WriteLine($"Executed: HLEN {key} -> {count}");
                    return $":{count}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }

            Console.WriteLine($"Executed: HLEN {key} -> 0 (key not found)");
            return ":0\r\n";
        }

        private string HandleDbsizeCommand(string[] commandArgs)
        {
            var count = KeyCount;
            Console.WriteLine($"Executed: DBSIZE -> {count}");
            return $":{count}\r\n";
        }

        private string HandleExpireCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 3)
            {
                return "-ERR wrong number of arguments for 'expire' command\r\n";
            }

            var key = commandArgs[1];
            if (!int.TryParse(commandArgs[2], out int seconds))
            {
                return "-ERR value is not an integer or out of range\r\n";
            }

            if (seconds <= 0)
            {
                return "-ERR invalid expire time in 'expire' command\r\n";
            }

            if (_Storage.TryGetValue(key, out var redisValue) && !redisValue.IsExpired)
            {
                redisValue.SetExpiration(seconds);
                Console.WriteLine($"Executed: EXPIRE {key} {seconds} -> 1 (expiration set)");
                return ":1\r\n";
            }

            Console.WriteLine($"Executed: EXPIRE {key} {seconds} -> 0 (key does not exist)");
            return ":0\r\n";
        }

        private string HandlePersistCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
            {
                return "-ERR wrong number of arguments for 'persist' command\r\n";
            }

            var key = commandArgs[1];

            if (_Storage.TryGetValue(key, out var redisValue) && !redisValue.IsExpired)
            {
                if (redisValue.ExpiresAt.HasValue)
                {
                    redisValue.RemoveExpiration();
                    Console.WriteLine($"Executed: PERSIST {key} -> 1 (expiration removed)");
                    return ":1\r\n";
                }
                else
                {
                    Console.WriteLine($"Executed: PERSIST {key} -> 0 (no expiration to remove)");
                    return ":0\r\n";
                }
            }

            Console.WriteLine($"Executed: PERSIST {key} -> 0 (key does not exist)");
            return ":0\r\n";
        }

        /// <summary>
        /// Handles the HSCAN command to scan hash fields.
        /// </summary>
        /// <param name="commandArgs">The command arguments.</param>
        /// <returns>The RESP response.</returns>
        private string HandleHscanCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 3)
                return "-ERR wrong number of arguments for 'hscan' command\r\n";

            var key = commandArgs[1];
            if (!int.TryParse(commandArgs[2], out var cursor))
                return "-ERR invalid cursor\r\n";

            // Parse optional MATCH and COUNT parameters
            string pattern = "*";
            int count = 10;
            
            for (int i = 3; i < commandArgs.Length; i += 2)
            {
                if (i + 1 >= commandArgs.Length) break;
                
                string param = commandArgs[i].ToUpper();
                if (param == "MATCH")
                {
                    pattern = commandArgs[i + 1];
                }
                else if (param == "COUNT")
                {
                    if (int.TryParse(commandArgs[i + 1], out int c))
                        count = c;
                }
            }

            if (!_Storage.TryGetValue(key, out var value) || value.IsExpired)
            {
                Console.WriteLine($"Executed: HSCAN {key} {cursor} -> empty hash (key does not exist)");
                return "*2\r\n$1\r\n0\r\n*0\r\n";
            }

            if (!(value is HashValue hashValue))
            {
                return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
            }

            var fields = hashValue.Fields.ToList();
            var matchingFields = new List<KeyValuePair<string, string>>();

            // Simple pattern matching (supports * wildcard)
            foreach (var field in fields)
            {
                if (IsPatternMatch(field.Key, pattern))
                {
                    matchingFields.Add(field);
                }
            }

            // For simplicity, return all matching fields (cursor always 0)
            var resultFields = matchingFields.Take(count).ToList();
            
            // Build RESP response: *2\r\n$cursor\r\n*array_of_fields\r\n
            var response = new System.Text.StringBuilder();
            response.Append("*2\r\n");
            response.Append("$1\r\n0\r\n"); // Next cursor (0 = scan complete)
            
            response.Append($"*{resultFields.Count * 2}\r\n");
            foreach (var field in resultFields)
            {
                response.Append($"${field.Key.Length}\r\n{field.Key}\r\n");
                response.Append($"${field.Value.Length}\r\n{field.Value}\r\n");
            }

            Console.WriteLine($"Executed: HSCAN {key} {cursor} -> {resultFields.Count} field pairs");
            return response.ToString();
        }

        /// <summary>
        /// Simple pattern matching for HSCAN MATCH parameter.
        /// </summary>
        /// <param name="text">The text to match.</param>
        /// <param name="pattern">The pattern (supports * wildcard).</param>
        /// <returns>True if the text matches the pattern.</returns>
        private bool IsPatternMatch(string text, string pattern)
        {
            if (pattern == "*") return true;
            if (pattern == text) return true;
            
            // Simple wildcard matching
            if (pattern.Contains("*"))
            {
                var parts = pattern.Split('*');
                if (parts.Length == 2)
                {
                    var prefix = parts[0];
                    var suffix = parts[1];
                    return text.StartsWith(prefix) && text.EndsWith(suffix) && text.Length >= prefix.Length + suffix.Length;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Handles the HEXISTS command to check if a hash field exists.
        /// </summary>
        /// <param name="commandArgs">The command arguments.</param>
        /// <returns>The RESP response.</returns>
        private string HandleHexistsCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 3)
                return "-ERR wrong number of arguments for 'hexists' command\r\n";

            var key = commandArgs[1];
            var field = commandArgs[2];

            if (!_Storage.TryGetValue(key, out var value) || value.IsExpired)
            {
                Console.WriteLine($"Executed: HEXISTS {key} {field} -> 0 (key does not exist)");
                return ":0\r\n";
            }

            if (!(value is HashValue hashValue))
            {
                return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
            }

            bool exists = hashValue.Fields.ContainsKey(field);
            Console.WriteLine($"Executed: HEXISTS {key} {field} -> {(exists ? 1 : 0)}");
            return $":{(exists ? 1 : 0)}\r\n";
        }

        /// <summary>
        /// Handles the KEYS command to list keys matching a pattern.
        /// </summary>
        /// <param name="commandArgs">The command arguments.</param>
        /// <returns>The RESP response.</returns>
        private string HandleKeysCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'keys' command\r\n";

            var pattern = commandArgs[1];
            var matchingKeys = new List<string>();

            foreach (var kvp in _Storage)
            {
                if (!kvp.Value.IsExpired && IsPatternMatch(kvp.Key, pattern))
                {
                    matchingKeys.Add(kvp.Key);
                }
            }

            var response = new System.Text.StringBuilder();
            response.Append($"*{matchingKeys.Count}\r\n");
            foreach (var key in matchingKeys)
            {
                response.Append($"${key.Length}\r\n{key}\r\n");
            }

            Console.WriteLine($"Executed: KEYS {pattern} -> {matchingKeys.Count} keys");
            return response.ToString();
        }

        /// <summary>
        /// Handles the EXISTS command to check if keys exist.
        /// </summary>
        /// <param name="commandArgs">The command arguments.</param>
        /// <returns>The RESP response.</returns>
        private string HandleExistsCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
                return "-ERR wrong number of arguments for 'exists' command\r\n";

            int count = 0;
            for (int i = 1; i < commandArgs.Length; i++)
            {
                var key = commandArgs[i];
                if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
                {
                    count++;
                }
            }

            Console.WriteLine($"Executed: EXISTS -> {count} existing keys");
            return $":{count}\r\n";
        }

        /// <summary>
        /// Handles the DEL command to delete keys.
        /// </summary>
        /// <param name="commandArgs">The command arguments.</param>
        /// <returns>The RESP response.</returns>
        private string HandleDelCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
                return "-ERR wrong number of arguments for 'del' command\r\n";

            int count = 0;
            for (int i = 1; i < commandArgs.Length; i++)
            {
                var key = commandArgs[i];
                if (_Storage.TryRemove(key, out _))
                {
                    count++;
                }
            }

            Console.WriteLine($"Executed: DEL -> {count} keys deleted");
            return $":{count}\r\n";
        }

        /// <summary>
        /// Handles the SCAN command to scan keys.
        /// </summary>
        /// <param name="commandArgs">The command arguments.</param>
        /// <returns>The RESP response.</returns>
        private string HandleScanCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
                return "-ERR wrong number of arguments for 'scan' command\r\n";

            if (!int.TryParse(commandArgs[1], out var cursor))
                return "-ERR invalid cursor\r\n";

            // Parse optional parameters
            string pattern = "*";
            int count = 10;
            
            for (int i = 2; i < commandArgs.Length; i += 2)
            {
                if (i + 1 >= commandArgs.Length) break;
                
                string param = commandArgs[i].ToUpper();
                if (param == "MATCH")
                {
                    pattern = commandArgs[i + 1];
                }
                else if (param == "COUNT")
                {
                    if (int.TryParse(commandArgs[i + 1], out int c))
                        count = c;
                }
            }

            var matchingKeys = new List<string>();
            foreach (var kvp in _Storage)
            {
                if (!kvp.Value.IsExpired && IsPatternMatch(kvp.Key, pattern))
                {
                    matchingKeys.Add(kvp.Key);
                }
            }

            var resultKeys = matchingKeys.Take(count).ToList();
            
            var response = new System.Text.StringBuilder();
            response.Append("*2\r\n");
            response.Append("$1\r\n0\r\n"); // Next cursor (0 = scan complete)
            
            response.Append($"*{resultKeys.Count}\r\n");
            foreach (var key in resultKeys)
            {
                response.Append($"${key.Length}\r\n{key}\r\n");
            }

            Console.WriteLine($"Executed: SCAN {cursor} -> {resultKeys.Count} keys");
            return response.ToString();
        }

        /// <summary>
        /// Handles the INCR command to increment a string value.
        /// </summary>
        /// <param name="commandArgs">The command arguments.</param>
        /// <returns>The RESP response.</returns>
        private string HandleIncrCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'incr' command\r\n";

            var key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is StringValue stringValue)
                {
                    try
                    {
                        long result = stringValue.Increment();
                        Console.WriteLine($"Executed: INCR {key} -> {result}");
                        return $":{result}\r\n";
                    }
                    catch
                    {
                        return "-ERR value is not an integer or out of range\r\n";
                    }
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                // Key doesn't exist, create with value 1
                var newStringValue = new StringValue("1");
                _Storage.AddOrUpdate(key, newStringValue, (k, v) => newStringValue);
                Console.WriteLine($"Executed: INCR {key} -> 1 (new key)");
                return ":1\r\n";
            }
        }

        /// <summary>
        /// Handles the DECR command to decrement a string value.
        /// </summary>
        /// <param name="commandArgs">The command arguments.</param>
        /// <returns>The RESP response.</returns>
        private string HandleDecrCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'decr' command\r\n";

            var key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is StringValue stringValue)
                {
                    try
                    {
                        long result = stringValue.Decrement();
                        Console.WriteLine($"Executed: DECR {key} -> {result}");
                        return $":{result}\r\n";
                    }
                    catch
                    {
                        return "-ERR value is not an integer or out of range\r\n";
                    }
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                // Key doesn't exist, create with value -1
                var newStringValue = new StringValue("-1");
                _Storage.AddOrUpdate(key, newStringValue, (k, v) => newStringValue);
                Console.WriteLine($"Executed: DECR {key} -> -1 (new key)");
                return ":-1\r\n";
            }
        }

        /// <summary>
        /// Handles the APPEND command to append to a string value.
        /// </summary>
        /// <param name="commandArgs">The command arguments.</param>
        /// <returns>The RESP response.</returns>
        private string HandleAppendCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 3)
                return "-ERR wrong number of arguments for 'append' command\r\n";

            var key = commandArgs[1];
            var text = commandArgs[2];
            
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is StringValue stringValue)
                {
                    int newLength = stringValue.Append(text);
                    Console.WriteLine($"Executed: APPEND {key} -> {newLength}");
                    return $":{newLength}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                // Key doesn't exist, create with the appended text
                var newStringValue = new StringValue(text);
                _Storage.AddOrUpdate(key, newStringValue, (k, v) => newStringValue);
                Console.WriteLine($"Executed: APPEND {key} -> {text.Length} (new key)");
                return $":{text.Length}\r\n";
            }
        }

        /// <summary>
        /// Handles the MGET command to get multiple values.
        /// </summary>
        /// <param name="commandArgs">The command arguments.</param>
        /// <returns>The RESP response.</returns>
        private string HandleMgetCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
                return "-ERR wrong number of arguments for 'mget' command\r\n";

            var response = new System.Text.StringBuilder();
            response.Append($"*{commandArgs.Length - 1}\r\n");
            
            for (int i = 1; i < commandArgs.Length; i++)
            {
                var key = commandArgs[i];
                if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
                {
                    if (value is StringValue stringValue)
                    {
                        response.Append($"${stringValue.Data.Length}\r\n{stringValue.Data}\r\n");
                    }
                    else
                    {
                        response.Append("$-1\r\n"); // Null for wrong type
                    }
                }
                else
                {
                    response.Append("$-1\r\n"); // Null for non-existent key
                }
            }

            Console.WriteLine($"Executed: MGET -> {commandArgs.Length - 1} keys requested");
            return response.ToString();
        }

        /// <summary>
        /// Handles the MSET command to set multiple values.
        /// </summary>
        /// <param name="commandArgs">The command arguments.</param>
        /// <returns>The RESP response.</returns>
        private string HandleMsetCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 3 || (commandArgs.Length - 1) % 2 != 0)
                return "-ERR wrong number of arguments for 'mset' command\r\n";

            int pairCount = (commandArgs.Length - 1) / 2;
            for (int i = 0; i < pairCount; i++)
            {
                var key = commandArgs[1 + i * 2];
                var value = commandArgs[2 + i * 2];
                var stringValue = new StringValue(value);
                _Storage.AddOrUpdate(key, stringValue, (k, v) => stringValue);
            }

            Console.WriteLine($"Executed: MSET -> {pairCount} key-value pairs set");
            return "+OK\r\n";
        }

        // List commands
        /// <summary>
        /// Handles the RPUSH command to push elements to the right of a list.
        /// </summary>
        private string HandleRpushCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 3)
                return "-ERR wrong number of arguments for 'rpush' command\r\n";

            var key = commandArgs[1];
            var values = commandArgs.Skip(2).ToArray();
            
            ListValue listValue;
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is ListValue existing)
                    listValue = existing;
                else
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
            }
            else
            {
                listValue = new ListValue();
                _Storage.AddOrUpdate(key, listValue, (k, v) => listValue);
            }

            int newLength = listValue.RPush(values);
            Console.WriteLine($"Executed: RPUSH {key} -> {newLength}");
            return $":{newLength}\r\n";
        }

        /// <summary>
        /// Handles the LPUSH command to push elements to the left of a list.
        /// </summary>
        private string HandleLpushCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 3)
                return "-ERR wrong number of arguments for 'lpush' command\r\n";

            var key = commandArgs[1];
            var values = commandArgs.Skip(2).ToArray();
            
            ListValue listValue;
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is ListValue existing)
                    listValue = existing;
                else
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
            }
            else
            {
                listValue = new ListValue();
                _Storage.AddOrUpdate(key, listValue, (k, v) => listValue);
            }

            int newLength = listValue.LPush(values);
            Console.WriteLine($"Executed: LPUSH {key} -> {newLength}");
            return $":{newLength}\r\n";
        }

        /// <summary>
        /// Handles the RPOP command to pop an element from the right of a list.
        /// </summary>
        private string HandleRpopCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'rpop' command\r\n";

            var key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is ListValue listValue)
                {
                    var popped = listValue.RPop();
                    if (popped == null)
                    {
                        Console.WriteLine($"Executed: RPOP {key} -> (nil)");
                        return "$-1\r\n";
                    }
                    Console.WriteLine($"Executed: RPOP {key} -> {popped}");
                    return $"${popped.Length}\r\n{popped}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: RPOP {key} -> (nil)");
                return "$-1\r\n";
            }
        }

        /// <summary>
        /// Handles the LPOP command to pop an element from the left of a list.
        /// </summary>
        private string HandleLpopCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'lpop' command\r\n";

            var key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is ListValue listValue)
                {
                    var popped = listValue.LPop();
                    if (popped == null)
                    {
                        Console.WriteLine($"Executed: LPOP {key} -> (nil)");
                        return "$-1\r\n";
                    }
                    Console.WriteLine($"Executed: LPOP {key} -> {popped}");
                    return $"${popped.Length}\r\n{popped}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: LPOP {key} -> (nil)");
                return "$-1\r\n";
            }
        }

        /// <summary>
        /// Handles the LRANGE command to get a range of elements from a list.
        /// </summary>
        private string HandleLrangeCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 4)
                return "-ERR wrong number of arguments for 'lrange' command\r\n";

            var key = commandArgs[1];
            if (!int.TryParse(commandArgs[2], out var start) || !int.TryParse(commandArgs[3], out var stop))
                return "-ERR value is not an integer or out of range\r\n";
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is ListValue listValue)
                {
                    var range = listValue.LRange(start, stop);
                    var response = new System.Text.StringBuilder();
                    response.Append($"*{range.Length}\r\n");
                    foreach (var item in range)
                    {
                        response.Append($"${item.Length}\r\n{item}\r\n");
                    }
                    Console.WriteLine($"Executed: LRANGE {key} {start} {stop} -> {range.Length} elements");
                    return response.ToString();
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: LRANGE {key} {start} {stop} -> empty list");
                return "*0\r\n";
            }
        }

        /// <summary>
        /// Handles the LLEN command to get the length of a list.
        /// </summary>
        private string HandleLlenCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'llen' command\r\n";

            var key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is ListValue listValue)
                {
                    int length = listValue.LLen();
                    Console.WriteLine($"Executed: LLEN {key} -> {length}");
                    return $":{length}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: LLEN {key} -> 0");
                return ":0\r\n";
            }
        }

        /// <summary>
        /// Handles the INCRBY command to increment a string value by a given amount.
        /// </summary>
        private string HandleIncrByCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 3)
                return "-ERR wrong number of arguments for 'incrby' command\r\n";

            var key = commandArgs[1];
            
            if (!long.TryParse(commandArgs[2], out long increment))
                return "-ERR value is not an integer or out of range\r\n";
            
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is StringValue stringValue)
                {
                    try
                    {
                        long result = stringValue.IncrementBy(increment);
                        Console.WriteLine($"Executed: INCRBY {key} {increment} -> {result}");
                        return $":{result}\r\n";
                    }
                    catch
                    {
                        return "-ERR value is not an integer or out of range\r\n";
                    }
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                var stringValue = new StringValue(increment.ToString());
                _Storage.AddOrUpdate(key, stringValue, (k, v) => stringValue);
                Console.WriteLine($"Executed: INCRBY {key} {increment} -> {increment} (new key)");
                return $":{increment}\r\n";
            }
        }

        /// <summary>
        /// Handles the HMSET command to set multiple hash field-value pairs.
        /// </summary>
        private string HandleHmsetCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 4 || (commandArgs.Length - 2) % 2 != 0)
                return "-ERR wrong number of arguments for 'hmset' command\r\n";

            var key = commandArgs[1];
            
            // Get or create hash
            HashValue hashValue;
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is HashValue existing)
                {
                    hashValue = existing;
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                hashValue = new HashValue();
                _Storage.AddOrUpdate(key, hashValue, (k, v) => hashValue);
            }

            // Set field-value pairs
            int pairCount = (commandArgs.Length - 2) / 2;
            for (int i = 0; i < pairCount; i++)
            {
                var field = commandArgs[2 + i * 2];
                var value = commandArgs[3 + i * 2];
                hashValue.SetField(field, value);
            }

            Console.WriteLine($"Executed: HMSET {key} -> {pairCount} field-value pairs set");
            return "+OK\r\n";
        }
    }
}