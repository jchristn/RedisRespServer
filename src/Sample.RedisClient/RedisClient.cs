namespace Sample.RedisClient
{
    using System;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A Redis client library that provides connection and command execution capabilities.
    /// </summary>
    /// <remarks>
    /// This class implements a Redis client library that can connect to any Redis server and execute
    /// Redis commands using the RESP protocol. It supports standard Redis commands like GET, SET, DEL, EXISTS, KEYS, PING, and FLUSHDB.
    /// </remarks>
    public class RedisClient : IDisposable
    {
        private static readonly bool _DebugLogging = false;

        /// <summary>
        /// Gets the hostname of the Redis server.
        /// </summary>
        /// <value>The server hostname or IP address.</value>
        public string Host { get; private set; }

        /// <summary>
        /// Gets the port number of the Redis server.
        /// </summary>
        /// <value>The server port number.</value>
        public int Port { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the client is currently connected to the server.
        /// </summary>
        /// <value>true if connected; otherwise, false.</value>
        public bool IsConnected => _TcpClient?.Connected ?? false;

        private TcpClient? _TcpClient;
        private NetworkStream? _Stream;
        private bool _Disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisClient"/> class.
        /// </summary>
        /// <param name="host">The hostname or IP address of the Redis server.</param>
        /// <param name="port">The port number of the Redis server. Defaults to 6379.</param>
        /// <exception cref="ArgumentNullException">Thrown when host is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when port is not in valid range.</exception>
        /// <remarks>
        /// Creates a new Redis client instance configured to connect to the specified server.
        /// Call <see cref="ConnectAsync"/> to establish the connection.
        /// </remarks>
        public RedisClient(string host, int port = 6379)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentNullException(nameof(host));
            
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

            Host = host;
            Port = port;
        }

        /// <summary>
        /// Establishes a connection to the Redis server.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous connect operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if already connected.</exception>
        /// <exception cref="SocketException">Thrown if connection fails.</exception>
        /// <remarks>
        /// This method establishes a TCP connection to the Redis server specified in the constructor.
        /// The connection will remain open until <see cref="DisconnectAsync"/> is called or the object is disposed.
        /// </remarks>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected)
                throw new InvalidOperationException("Client is already connected.");

            try
            {
                _TcpClient = new TcpClient();
                await _TcpClient.ConnectAsync(Host, Port).ConfigureAwait(false);
                _Stream = _TcpClient.GetStream();
                
                Console.WriteLine($"Connected to Redis server at {Host}:{Port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to Redis server: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Disconnects from the Redis server and cleans up resources.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous disconnect operation.</returns>
        /// <remarks>
        /// This method gracefully closes the connection to the Redis server and releases
        /// associated network resources. The client can be reconnected after disconnection.
        /// </remarks>
        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected) return;

            try
            {
                _Stream?.Close();
                _TcpClient?.Close();
                Console.WriteLine("Disconnected from Redis server.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during disconnect: {ex.Message}");
            }
            finally
            {
                _Stream?.Dispose();
                _TcpClient?.Dispose();
                _Stream = null;
                _TcpClient = null;
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a Redis command and displays the response.
        /// </summary>
        /// <param name="command">The Redis command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous command execution.</returns>
        /// <exception cref="ArgumentNullException">Thrown when command is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a Redis command to the server using the RESP protocol,
        /// reads the response, and displays the parsed result to the console.
        /// The command is automatically formatted according to RESP protocol specifications.
        /// </remarks>
        public async Task ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentNullException(nameof(command));

            if (!IsConnected)
                throw new InvalidOperationException("Not connected to server.");

            try
            {
                var respCommand = FormatAsRespCommand(command);
                var requestData = Encoding.UTF8.GetBytes(respCommand);
                
                await _Stream!.WriteAsync(requestData, 0, requestData.Length, cancellationToken).ConfigureAwait(false);
                await _Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                var response = await ReadResponseAsync(cancellationToken).ConfigureAwait(false);
                DisplayResponse(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute command '{command}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Executes a Redis command and returns the parsed response.
        /// </summary>
        /// <param name="command">The Redis command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task that returns the parsed response object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when command is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a Redis command to the server and returns the parsed response
        /// without displaying it to the console. Useful for programmatic access to responses.
        /// </remarks>
        public async Task<RedisResponse> ExecuteCommandWithResponseAsync(string command, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentNullException(nameof(command));

            if (!IsConnected)
                throw new InvalidOperationException("Not connected to server.");

            var respCommand = FormatAsRespCommand(command);
            var requestData = Encoding.UTF8.GetBytes(respCommand);
            
            await _Stream!.WriteAsync(requestData, 0, requestData.Length, cancellationToken).ConfigureAwait(false);
            await _Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            return await ReadResponseAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a PING command to test server connectivity.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous ping operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a PING command to the Redis server to test connectivity.
        /// A properly functioning server should respond with "PONG".
        /// </remarks>
        public async Task PingAsync(CancellationToken cancellationToken = default)
        {
            await ExecuteCommandAsync("PING", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves the value of the specified key.
        /// </summary>
        /// <param name="key">The key to retrieve.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous get operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a GET command to retrieve the value associated with the specified key.
        /// </remarks>
        public async Task GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            await ExecuteCommandAsync($"GET {key}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the value of the specified key.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous set operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a SET command to store the specified value under the given key.
        /// </remarks>
        public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            await ExecuteCommandAsync($"SET {key} {value ?? ""}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the specified key.
        /// </summary>
        /// <param name="key">The key to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous delete operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a DEL command to remove the specified key and its associated value.
        /// </remarks>
        public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            await ExecuteCommandAsync($"DEL {key}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if the specified key exists.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous exists operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an EXISTS command to check if the specified key exists in the database.
        /// </remarks>
        public async Task ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            await ExecuteCommandAsync($"EXISTS {key}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Lists all keys matching the specified pattern.
        /// </summary>
        /// <param name="pattern">The pattern to match keys against. Defaults to "*" (all keys).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous keys operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a KEYS command to retrieve all keys matching the specified pattern.
        /// Use with caution on large databases as it can be expensive.
        /// </remarks>
        public async Task KeysAsync(string pattern = "*", CancellationToken cancellationToken = default)
        {
            await ExecuteCommandAsync($"KEYS {pattern ?? "*"}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Flushes (deletes) all keys from the current database.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous flush operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a FLUSHDB command to remove all keys from the current database.
        /// This operation cannot be undone - use with caution.
        /// </remarks>
        public async Task FlushDatabaseAsync(CancellationToken cancellationToken = default)
        {
            await ExecuteCommandAsync("FLUSHDB", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets multiple key-value pairs atomically.
        /// </summary>
        /// <param name="keyValuePairs">Pairs of keys and values to set.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous MSET operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when keyValuePairs is null.</exception>
        /// <exception cref="ArgumentException">Thrown when keyValuePairs is empty or contains null keys.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an MSET command to set multiple key-value pairs atomically.
        /// All keys are set together as a single operation.
        /// </remarks>
        public async Task MsetAsync(params (string key, string value)[] keyValuePairs)
        {
            await MsetAsync(CancellationToken.None, keyValuePairs).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets multiple key-value pairs atomically.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <param name="keyValuePairs">Pairs of keys and values to set.</param>
        /// <returns>A task representing the asynchronous MSET operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when keyValuePairs is null.</exception>
        /// <exception cref="ArgumentException">Thrown when keyValuePairs is empty or contains null keys.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an MSET command to set multiple key-value pairs atomically.
        /// All keys are set together as a single operation.
        /// </remarks>
        public async Task MsetAsync(CancellationToken cancellationToken, params (string key, string value)[] keyValuePairs)
        {
            if (keyValuePairs == null)
                throw new ArgumentNullException(nameof(keyValuePairs));
            if (keyValuePairs.Length == 0)
                throw new ArgumentException("At least one key-value pair is required.", nameof(keyValuePairs));

            var commandParts = new List<string> { "MSET" };
            foreach (var (key, value) in keyValuePairs)
            {
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentException("Keys cannot be null or empty.", nameof(keyValuePairs));
                commandParts.Add(key);
                commandParts.Add(value ?? "");
            }

            await ExecuteCommandAsync(string.Join(" ", commandParts), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the values of multiple keys.
        /// </summary>
        /// <param name="keys">The keys to get values for.</param>
        /// <returns>A task representing the asynchronous MGET operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when keys is null.</exception>
        /// <exception cref="ArgumentException">Thrown when keys is empty or contains null keys.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an MGET command to get multiple key values in a single request.
        /// Returns null for keys that don't exist.
        /// </remarks>
        public async Task MgetAsync(params string[] keys)
        {
            await MgetAsync(CancellationToken.None, keys).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the values of multiple keys.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <param name="keys">The keys to get values for.</param>
        /// <returns>A task representing the asynchronous MGET operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when keys is null.</exception>
        /// <exception cref="ArgumentException">Thrown when keys is empty or contains null keys.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an MGET command to get multiple key values in a single request.
        /// Returns null for keys that don't exist.
        /// </remarks>
        public async Task MgetAsync(CancellationToken cancellationToken, params string[] keys)
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));
            if (keys.Length == 0)
                throw new ArgumentException("At least one key is required.", nameof(keys));

            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentException("Keys cannot be null or empty.", nameof(keys));
            }

            var command = $"MGET {string.Join(" ", keys)}";
            await ExecuteCommandAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Increments the numeric value of a key by one.
        /// </summary>
        /// <param name="key">The key to increment.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous INCR operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an INCR command to increment the key's value by 1.
        /// If the key doesn't exist, it's set to 0 before incrementing.
        /// </remarks>
        public async Task IncrAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            await ExecuteCommandAsync($"INCR {key}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Increments the numeric value of a key by the given amount.
        /// </summary>
        /// <param name="key">The key to increment.</param>
        /// <param name="increment">The amount to increment by.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous INCRBY operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an INCRBY command to increment the key's value by the specified amount.
        /// If the key doesn't exist, it's set to 0 before incrementing.
        /// </remarks>
        public async Task IncrByAsync(string key, long increment, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            await ExecuteCommandAsync($"INCRBY {key} {increment}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Decrements the numeric value of a key by one.
        /// </summary>
        /// <param name="key">The key to decrement.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous DECR operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a DECR command to decrement the key's value by 1.
        /// If the key doesn't exist, it's set to 0 before decrementing.
        /// </remarks>
        public async Task DecrAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            await ExecuteCommandAsync($"DECR {key}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets multiple hash field-value pairs.
        /// </summary>
        /// <param name="key">The hash key.</param>
        /// <param name="fieldValuePairs">Pairs of fields and values to set.</param>
        /// <returns>A task representing the asynchronous HMSET operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key or fieldValuePairs is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or fieldValuePairs is empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an HMSET command to set multiple field-value pairs in a hash.
        /// If the hash doesn't exist, it will be created.
        /// </remarks>
        public async Task HmsetAsync(string key, params (string field, string value)[] fieldValuePairs)
        {
            await HmsetAsync(key, CancellationToken.None, fieldValuePairs).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets multiple hash field-value pairs.
        /// </summary>
        /// <param name="key">The hash key.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <param name="fieldValuePairs">Pairs of fields and values to set.</param>
        /// <returns>A task representing the asynchronous HMSET operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key or fieldValuePairs is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or fieldValuePairs is empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an HMSET command to set multiple field-value pairs in a hash.
        /// If the hash doesn't exist, it will be created.
        /// </remarks>
        public async Task HmsetAsync(string key, CancellationToken cancellationToken, params (string field, string value)[] fieldValuePairs)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
            if (fieldValuePairs == null)
                throw new ArgumentNullException(nameof(fieldValuePairs));
            if (fieldValuePairs.Length == 0)
                throw new ArgumentException("At least one field-value pair is required.", nameof(fieldValuePairs));

            var commandParts = new List<string> { "HMSET", key };
            foreach (var (field, value) in fieldValuePairs)
            {
                if (string.IsNullOrEmpty(field))
                    throw new ArgumentException("Fields cannot be null or empty.", nameof(fieldValuePairs));
                commandParts.Add(field);
                commandParts.Add(value ?? "");
            }

            await ExecuteCommandAsync(string.Join(" ", commandParts), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the client name for the current connection.
        /// </summary>
        /// <param name="name">The client name to set.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous client setname operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a CLIENT SETNAME command to set the connection name.
        /// </remarks>
        public async Task ClientSetNameAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            await ExecuteCommandAsync($"CLIENT SETNAME {name}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets client library information.
        /// </summary>
        /// <param name="attribute">The attribute to set (lib-name or lib-ver).</param>
        /// <param name="value">The value to set for the attribute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous client setinfo operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when attribute or value is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a CLIENT SETINFO command to set client library information.
        /// </remarks>
        public async Task ClientSetInfoAsync(string attribute, string value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(attribute))
                throw new ArgumentNullException(nameof(attribute));
            if (string.IsNullOrEmpty(value))
                throw new ArgumentNullException(nameof(value));

            await ExecuteCommandAsync($"CLIENT SETINFO {attribute} {value}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the client ID of the current connection.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous client ID operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a CLIENT ID command to get the unique client ID.
        /// </remarks>
        public async Task ClientIdAsync(CancellationToken cancellationToken = default)
        {
            await ExecuteCommandAsync("CLIENT ID", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets configuration parameters from the server.
        /// </summary>
        /// <param name="parameter">The configuration parameter to retrieve.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous config get operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when parameter is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a CONFIG GET command to retrieve server configuration.
        /// </remarks>
        public async Task ConfigGetAsync(string parameter, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(parameter))
                throw new ArgumentNullException(nameof(parameter));

            await ExecuteCommandAsync($"CONFIG GET {parameter}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets information about sentinel masters.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous sentinel masters operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a SENTINEL MASTERS command to get sentinel information.
        /// </remarks>
        public async Task SentinelMastersAsync(CancellationToken cancellationToken = default)
        {
            await ExecuteCommandAsync("SENTINEL MASTERS", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets server information.
        /// </summary>
        /// <param name="section">The info section to retrieve (optional).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous info operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an INFO command to get server information.
        /// </remarks>
        public async Task InfoAsync(string? section = null, CancellationToken cancellationToken = default)
        {
            var command = string.IsNullOrEmpty(section) ? "INFO" : $"INFO {section}";
            await ExecuteCommandAsync(command, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets cluster nodes information.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous cluster nodes operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a CLUSTER NODES command to get cluster information.
        /// </remarks>
        public async Task ClusterNodesAsync(CancellationToken cancellationToken = default)
        {
            await ExecuteCommandAsync("CLUSTER NODES", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Subscribes to a Redis channel.
        /// </summary>
        /// <param name="channel">The channel to subscribe to.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous subscribe operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when channel is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends a SUBSCRIBE command to subscribe to a channel.
        /// </remarks>
        public async Task SubscribeAsync(string channel, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(channel))
                throw new ArgumentNullException(nameof(channel));

            await ExecuteCommandAsync($"SUBSCRIBE {channel}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends an ECHO command with the specified message.
        /// </summary>
        /// <param name="message">The message to echo.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous echo operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when message is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an ECHO command to echo back the specified message.
        /// </remarks>
        public async Task EchoAsync(string message, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullException(nameof(message));

            await ExecuteCommandAsync($"ECHO {message}", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends an ECHO command with binary data.
        /// </summary>
        /// <param name="data">The binary data to echo.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous echo operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method sends an ECHO command with binary data using RESP protocol.
        /// </remarks>
        public async Task EchoBinaryAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (!IsConnected)
                throw new InvalidOperationException("Not connected to server.");

            try
            {
                // Format as RESP command with binary data
                var sb = new StringBuilder();
                sb.AppendLine("*2");
                sb.AppendLine("$4");
                sb.AppendLine("ECHO");
                sb.AppendLine($"${data.Length}");

                var commandPrefix = Encoding.UTF8.GetBytes(sb.ToString());
                var commandSuffix = Encoding.UTF8.GetBytes("\r\n");

                // Write command prefix
                await _Stream!.WriteAsync(commandPrefix, 0, commandPrefix.Length, cancellationToken).ConfigureAwait(false);
                // Write binary data
                await _Stream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
                // Write command suffix
                await _Stream.WriteAsync(commandSuffix, 0, commandSuffix.Length, cancellationToken).ConfigureAwait(false);
                await _Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                var response = await ReadResponseAsync(cancellationToken).ConfigureAwait(false);
                DisplayResponse(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute binary ECHO command: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Runs a sequence of commands similar to what StackExchange.Redis sends on connection.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous StackExchange.Redis simulation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected to server.</exception>
        /// <remarks>
        /// This method simulates the connection sequence that StackExchange.Redis performs,
        /// including client identification, configuration queries, and test commands.
        /// </remarks>
        public async Task RunStackExchangeRedisSequenceAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("=== Running StackExchange.Redis Connection Sequence ===");
            Console.WriteLine();

            try
            {
                // 1. Set client name
                Console.WriteLine("Setting client name...");
                await ClientSetNameAsync("JOEL-LAPTOP(SE.Redis-v2.9.11.19757)", cancellationToken).ConfigureAwait(false);

                // 2. Set client library info
                Console.WriteLine("Setting client library name...");
                await ClientSetInfoAsync("lib-name", "SE.Redis", cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Setting client library version...");
                await ClientSetInfoAsync("lib-ver", "2.9.11.19757", cancellationToken).ConfigureAwait(false);

                // 3. Get client ID
                Console.WriteLine("Getting client ID...");
                await ClientIdAsync(cancellationToken).ConfigureAwait(false);

                // 4. Configuration queries
                Console.WriteLine("Checking slave-read-only configuration...");
                await ConfigGetAsync("slave-read-only", cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Getting databases configuration...");
                await ConfigGetAsync("databases", cancellationToken).ConfigureAwait(false);

                // 5. Sentinel and cluster checks
                Console.WriteLine("Checking sentinel masters...");
                await SentinelMastersAsync(cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Getting replication info...");
                await InfoAsync("replication", cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Getting server info...");
                await InfoAsync("server", cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Checking cluster nodes...");
                await ClusterNodesAsync(cancellationToken).ConfigureAwait(false);

                // 6. Tiebreaker check
                Console.WriteLine("Checking tiebreaker...");
                await GetAsync("__Booksleeve_TieBreak", cancellationToken).ConfigureAwait(false);

                // 7. Subscribe to master changed notifications
                Console.WriteLine("Subscribing to master changed notifications...");
                await SubscribeAsync("__Booksleeve_MasterChanged", cancellationToken).ConfigureAwait(false);

                // 8. Echo test with binary data (simulating handshake tracer)
                Console.WriteLine("Sending echo handshake tracer...");
                var binaryData = new byte[] { 0x45, 0x3A, 0x72, 0x78, 0x77, 0x3F, 0x4F, 0x48, 0x3F, 0x3F, 0x69, 0x55, 0x3E, 0x22, 0x61, 0xB5 };
                await EchoBinaryAsync(binaryData, cancellationToken).ConfigureAwait(false);

                // 9. Get replication info again
                Console.WriteLine("Getting replication info again...");
                await InfoAsync("replication", cancellationToken).ConfigureAwait(false);

                // 10. Test commands similar to the log
                Console.WriteLine();
                Console.WriteLine("=== Running Test Commands ===");
                
                Console.WriteLine("Setting foo = bar...");
                await SetAsync("foo", "bar", cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Getting foo...");
                await GetAsync("foo", cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Checking if foo exists...");
                await ExistsAsync("foo", cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Checking if bar exists...");
                await ExistsAsync("bar", cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Setting counter = 32...");
                await SetAsync("counter", "32", cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Getting counter...");
                await GetAsync("counter", cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Pinging server...");
                await PingAsync(cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Flushing database...");
                await FlushDatabaseAsync(cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Checking if foo exists after flush...");
                await ExistsAsync("foo", cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Setting foo = bar again...");
                await SetAsync("foo", "bar", cancellationToken).ConfigureAwait(false);

                Console.WriteLine();
                Console.WriteLine("=== StackExchange.Redis Sequence Complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StackExchange.Redis sequence: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="RedisClient"/>.
        /// </summary>
        /// <remarks>
        /// This method disconnects from the server if connected and releases all network resources.
        /// After calling this method, the client instance cannot be reused.
        /// </remarks>
        public void Dispose()
        {
            if (_Disposed) return;

            try
            {
                DisconnectAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore errors during disposal
            }

            _Disposed = true;
        }

        private async Task<RedisResponse> ReadResponseAsync(CancellationToken cancellationToken = default)
        {
            var buffer = new byte[4096];
            var responseBuilder = new StringBuilder();
            
            while (true)
            {
                var bytesRead = await _Stream!.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                    throw new InvalidOperationException("Connection closed by server");

                var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                responseBuilder.Append(data);

                var response = responseBuilder.ToString();
                if (IsCompleteResponse(response))
                {
                    return ParseResponse(response);
                }
            }
        }

        private bool IsCompleteResponse(string response)
        {
            if (string.IsNullOrEmpty(response)) return false;
            
            var firstChar = response[0];
            
            return firstChar switch
            {
                '+' or '-' or ':' => response.Contains("\r\n"),
                '$' => ParseBulkStringComplete(response),
                '*' => ParseArrayComplete(response),
                _ => false
            };
        }

        private bool ParseBulkStringComplete(string response)
        {
            if (!response.StartsWith("$")) return false;
            
            // Find the first \r\n to get the length line
            var firstCrLf = response.IndexOf("\r\n");
            if (firstCrLf == -1) return false;
            
            var lengthLine = response[..firstCrLf];
            if (!int.TryParse(lengthLine[1..], out var length))
                return false;
            
            if (length == -1) 
                return response.Length >= firstCrLf + 2; // $-1\r\n (just need the final \r\n)
            
            // Calculate expected total response length: $<length>\r\n<content>\r\n
            var expectedLength = lengthLine.Length + 2 + length + 2; // header + \r\n + content + \r\n
            return response.Length >= expectedLength;
        }

        private bool ParseArrayComplete(string response)
        {
            if (!response.StartsWith("*")) return false;
            
            // Find the first \r\n to get the count line
            var firstCrLf = response.IndexOf("\r\n");
            if (firstCrLf == -1) return false;
            
            var countLine = response[..firstCrLf];
            if (!int.TryParse(countLine[1..], out var count))
                return false;
            
            if (count == -1) return true; // Null array
            
            var position = firstCrLf + 2; // Skip past the first \r\n
            
            // Check if we have all array elements
            for (int i = 0; i < count; i++)
            {
                if (position >= response.Length) return false;
                
                var elementType = response[position];
                
                // Calculate where this element should end
                switch (elementType)
                {
                    case '+': // Simple string
                    case '-': // Error  
                    case ':': // Integer
                        var elementEnd = response.IndexOf("\r\n", position);
                        if (elementEnd == -1) return false;
                        position = elementEnd + 2;
                        break;
                        
                    case '$': // Bulk string
                        var lengthEnd = response.IndexOf("\r\n", position);
                        if (lengthEnd == -1) return false;
                        
                        var lengthStr = response[(position + 1)..lengthEnd];
                        if (!int.TryParse(lengthStr, out var bulkLength)) return false;
                        
                        if (bulkLength == -1)
                        {
                            position = lengthEnd + 2; // Just $-1\r\n
                        }
                        else
                        {
                            var expectedEnd = lengthEnd + 2 + bulkLength + 2; // $<len>\r\n<content>\r\n
                            if (response.Length < expectedEnd) return false;
                            position = expectedEnd;
                        }
                        break;
                        
                    case '*': // Nested array
                        return false; // Simplified - don't support nested arrays for completion check
                        
                    default:
                        return false; // Unknown element type
                }
            }
            
            return true; // All elements accounted for
        }

        private RedisResponse ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return new RedisResponse(RedisResponseType.Error, "Empty response");

            var firstChar = response[0];
            
            return firstChar switch
            {
                '+' => ParseSimpleString(response),
                '-' => ParseError(response),
                ':' => ParseInteger(response),
                '$' => ParseBulkString(response),
                '*' => ParseArray(response),
                _ => new RedisResponse(RedisResponseType.Error, $"Unknown response type: {firstChar}")
            };
        }

        private RedisResponse ParseSimpleString(string response)
        {
            var content = response[1..].TrimEnd('\r', '\n');
            return new RedisResponse(RedisResponseType.SimpleString, content);
        }

        private RedisResponse ParseError(string response)
        {
            var content = response[1..].TrimEnd('\r', '\n');
            return new RedisResponse(RedisResponseType.Error, content);
        }

        private RedisResponse ParseInteger(string response)
        {
            var content = response[1..].TrimEnd('\r', '\n');
            if (long.TryParse(content, out var value))
            {
                return new RedisResponse(RedisResponseType.Integer, value);
            }
            return new RedisResponse(RedisResponseType.Error, $"Invalid integer: {content}");
        }

        private RedisResponse ParseBulkString(string response)
        {
            if (!response.StartsWith("$"))
                return new RedisResponse(RedisResponseType.Error, "Invalid bulk string format");

            // Find the first \r\n to get the length line
            var firstCrLf = response.IndexOf("\r\n");
            if (firstCrLf == -1)
                return new RedisResponse(RedisResponseType.Error, "Invalid bulk string format");

            var lengthLine = response[..firstCrLf];
            if (!int.TryParse(lengthLine[1..], out var length))
                return new RedisResponse(RedisResponseType.Error, "Invalid bulk string length");

            if (length == -1)
                return new RedisResponse(RedisResponseType.Null, null);

            // Extract content starting after the first \r\n
            var contentStartIndex = firstCrLf + 2;
            if (response.Length < contentStartIndex + length)
                return new RedisResponse(RedisResponseType.Error, "Incomplete bulk string");

            var content = response.Substring(contentStartIndex, length);
            return new RedisResponse(RedisResponseType.BulkString, content);
        }

        private RedisResponse ParseArray(string response)
        {
            if (!response.StartsWith("*"))
                return new RedisResponse(RedisResponseType.Error, "Invalid array format");

            // Find the first \r\n to get the count line
            var firstCrLf = response.IndexOf("\r\n");
            if (firstCrLf == -1)
                return new RedisResponse(RedisResponseType.Error, "Invalid array format");

            var countLine = response[..firstCrLf];
            if (!int.TryParse(countLine[1..], out var count))
                return new RedisResponse(RedisResponseType.Error, "Invalid array count");

            if (count == -1)
                return new RedisResponse(RedisResponseType.Null, null);

            var elements = new List<object>();
            var position = firstCrLf + 2; // Skip past the first \r\n

            for (int i = 0; i < count; i++)
            {
                if (position >= response.Length)
                    return new RedisResponse(RedisResponseType.Error, "Incomplete array");

                // Determine the type of the current element
                var elementType = response[position];
                var elementEnd = position;

                // Find the end of this element based on its type
                switch (elementType)
                {
                    case '+': // Simple string
                    case '-': // Error
                    case ':': // Integer
                        elementEnd = response.IndexOf("\r\n", position);
                        if (elementEnd == -1) 
                            return new RedisResponse(RedisResponseType.Error, "Incomplete array element");
                        elementEnd += 2; // Include the \r\n
                        break;

                    case '$': // Bulk string
                        var lengthEnd = response.IndexOf("\r\n", position);
                        if (lengthEnd == -1)
                            return new RedisResponse(RedisResponseType.Error, "Incomplete bulk string length");
                        
                        var lengthStr = response[(position + 1)..lengthEnd];
                        if (!int.TryParse(lengthStr, out var bulkLength))
                            return new RedisResponse(RedisResponseType.Error, "Invalid bulk string length");
                        
                        if (bulkLength == -1)
                        {
                            elementEnd = lengthEnd + 2; // Just $-1\r\n
                        }
                        else
                        {
                            elementEnd = lengthEnd + 2 + bulkLength + 2; // $<len>\r\n<content>\r\n
                        }
                        break;

                    case '*': // Nested array - recursively calculate size
                        var nestedCountEnd = response.IndexOf("\r\n", position);
                        if (nestedCountEnd == -1)
                            return new RedisResponse(RedisResponseType.Error, "Incomplete nested array");
                        
                        // For simplicity, find the end by recursively parsing
                        // This is a simplified approach - in practice you'd need full recursive parsing
                        return new RedisResponse(RedisResponseType.Error, "Nested arrays not fully supported");

                    default:
                        return new RedisResponse(RedisResponseType.Error, $"Unknown element type: {elementType}");
                }

                // Extract the element response
                var elementResponse = response[position..elementEnd];
                var elementResult = ParseResponse(elementResponse);
                elements.Add(elementResult.Value ?? "(null)");
                
                position = elementEnd;
            }

            return new RedisResponse(RedisResponseType.Array, elements);
        }

        private void DisplayResponse(RedisResponse response)
        {
            Console.WriteLine($"Response: {FormatResponse(response)}");
            
            if (_DebugLogging)
            {
                Console.WriteLine($"DEBUG Response type: {response.Type}");
                if (response.Value != null)
                {
                    Console.WriteLine($"DEBUG Response value type: {response.Value.GetType().Name}");
                    Console.WriteLine($"DEBUG Raw response value: {response.Value}");
                }
            }
        }

        private string FormatResponse(RedisResponse response)
        {
            return response.Type switch
            {
                RedisResponseType.SimpleString => $"(string) \"{response.Value}\"",
                RedisResponseType.Error => $"(error) {response.Value}",
                RedisResponseType.Integer => $"(integer) {response.Value}",
                RedisResponseType.BulkString => response.Value != null ? $"(string) \"{response.Value}\"" : "(nil)",
                RedisResponseType.Null => "(nil)",
                RedisResponseType.Array => FormatArray(response.Value as List<object>),
                _ => $"(unknown) {response.Value}"
            };
        }

        private string FormatArray(List<object>? array)
        {
            if (array == null || array.Count == 0)
                return "(empty list or set)";

            var result = new StringBuilder();
            for (int i = 0; i < array.Count; i++)
            {
                result.AppendLine($"{i + 1}) \"{array[i]}\"");
            }
            return result.ToString().TrimEnd();
        }

        private string FormatAsRespCommand(string command)
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            
            sb.AppendLine($"*{parts.Length}");
            
            foreach (var part in parts)
            {
                sb.AppendLine($"${part.Length}");
                sb.AppendLine(part);
            }
            
            return sb.ToString();
        }
    }
}