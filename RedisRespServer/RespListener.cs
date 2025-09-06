namespace RedisResp
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// A TCP server that listens for and parses Redis Serialization Protocol (RESP) messages.
    /// Provides event-driven notification for different types of RESP data and client connections.
    /// </summary>
    /// <remarks>
    /// This class implements a multi-client TCP server that can parse RESP protocol messages
    /// and raise appropriate events based on the data type received. It supports all standard
    /// RESP data types including simple strings, errors, integers, bulk strings, arrays, and null values.
    /// </remarks>
    public class RespListener : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Raised when a simple string (prefix: +) is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> SimpleStringReceived;

        /// <summary>
        /// Raised when an error message (prefix: -) is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> ErrorReceived;

        /// <summary>
        /// Raised when an integer value (prefix: :) is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> IntegerReceived;

        /// <summary>
        /// Raised when a bulk string (prefix: $) is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> BulkStringReceived;

        /// <summary>
        /// Raised when an array (prefix: *) is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> ArrayReceived;

        /// <summary>
        /// Raised when a null value is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> NullReceived;

        /// <summary>
        /// Raised when any RESP data is received from a client, regardless of type.
        /// </summary>
        /// <remarks>
        /// This is a generic event that fires for all data types. Use this for logging
        /// or monitoring all incoming data without subscribing to individual type events.
        /// </remarks>
        public event EventHandler<RespDataReceivedEventArgs> DataReceived;

        /// <summary>
        /// Raised when a new client connects to the server.
        /// </summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;

        /// <summary>
        /// Raised when a client disconnects from the server.
        /// </summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>
        /// Raised when an error occurs in the server or during client handling.
        /// </summary>
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Gets the port number on which the server is configured to listen.
        /// </summary>
        /// <value>The TCP port number.</value>
        public int Port { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the server is currently listening for connections.
        /// </summary>
        /// <value>true if the server is listening; otherwise, false.</value>
        public bool IsListening => _isListening;

        /// <summary>
        /// Gets the current number of connected clients.
        /// </summary>
        /// <value>The count of active client connections.</value>
        /// <remarks>This property is thread-safe.</remarks>
        public int ConnectedClientsCount
        {
            get
            {
                lock (_clientsLock)
                {
                    return _clients.Count;
                }
            }
        }

        #endregion

        #region Private-Members

        private TcpListener _listener;
        private bool _isListening;
        private readonly Dictionary<string, TcpClient> _clients = new();
        private readonly object _clientsLock = new object();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="RespListener"/> class.
        /// </summary>
        /// <param name="port">The port number to listen on. Defaults to 6379 (standard Redis port).</param>
        /// <remarks>
        /// The server will listen on all available network interfaces (IPAddress.Any).
        /// Call <see cref="StartAsync"/> to begin accepting client connections.
        /// </remarks>
        public RespListener(int port = 6379)
        {
            Port = port;
            _listener = new TcpListener(IPAddress.Any, port);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Starts the server and begins listening for client connections asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous start operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the server is already listening.</exception>
        /// <exception cref="SocketException">Thrown if the port is already in use or other socket errors occur.</exception>
        /// <remarks>
        /// This method starts the TCP listener and begins accepting client connections in the background.
        /// The method returns immediately after starting the listener; client connections are handled asynchronously.
        /// </remarks>
        public async Task StartAsync()
        {
            if (_isListening) return;

            try
            {
                _listener.Start();
                _isListening = true;

                Console.WriteLine($"Redis Protocol Listener started on port {Port}");

                // Start accepting clients
                _ = Task.Run(AcceptClientsAsync);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Message = "Failed to start listener",
                    Exception = ex
                });
                throw;
            }
        }

        /// <summary>
        /// Stops the server and disconnects all clients.
        /// </summary>
        /// <remarks>
        /// This method stops accepting new connections, closes all existing client connections,
        /// and stops the TCP listener. All connected clients will be disconnected gracefully.
        /// </remarks>
        public void Stop()
        {
            if (!_isListening) return;

            _isListening = false;
            _listener?.Stop();

            // Disconnect all clients
            lock (_clientsLock)
            {
                foreach (var kvp in _clients)
                {
                    kvp.Value.Close();
                    OnClientDisconnected(new ClientDisconnectedEventArgs
                    {
                        ClientId = kvp.Key,
                        Reason = "Server shutdown"
                    });
                }
                _clients.Clear();
            }

            Console.WriteLine("Redis Protocol Listener stopped");
        }

        /// <summary>
        /// Releases all resources used by the <see cref="RespListener"/>.
        /// </summary>
        /// <remarks>
        /// This method stops the server if it's running and disposes of the TCP listener.
        /// After calling this method, the instance cannot be reused.
        /// </remarks>
        public void Dispose()
        {
            Stop();
            _listener?.Stop();
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Continuously accepts new client connections while the server is listening.
        /// </summary>
        /// <returns>A task representing the asynchronous accept operation.</returns>
        private async Task AcceptClientsAsync()
        {
            while (_isListening)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    var clientId = Guid.NewGuid().ToString();

                    lock (_clientsLock)
                    {
                        _clients[clientId] = tcpClient;
                    }

                    OnClientConnected(new ClientConnectedEventArgs
                    {
                        ClientId = clientId,
                        RemoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint
                    });

                    // Handle client in separate task
                    _ = Task.Run(() => HandleClientAsync(clientId, tcpClient));
                }
                catch (ObjectDisposedException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    OnErrorOccurred(new ErrorEventArgs
                    {
                        Message = "Error accepting client",
                        Exception = ex
                    });
                }
            }
        }

        /// <summary>
        /// Handles communication with a specific client connection.
        /// </summary>
        /// <param name="clientId">The unique identifier for the client.</param>
        /// <param name="client">The TCP client connection.</param>
        /// <returns>A task representing the asynchronous client handling operation.</returns>
        private async Task HandleClientAsync(string clientId, TcpClient client)
        {
            var buffer = new byte[4096];
            var dataBuffer = new StringBuilder();

            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    while (client.Connected && _isListening)
                    {
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        dataBuffer.Append(data);

                        // Process complete RESP messages
                        await ProcessRespData(clientId, dataBuffer);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Message = "Error handling client",
                    Exception = ex,
                    ClientId = clientId
                });
            }
            finally
            {
                lock (_clientsLock)
                {
                    _clients.Remove(clientId);
                }

                OnClientDisconnected(new ClientDisconnectedEventArgs
                {
                    ClientId = clientId,
                    Reason = "Client disconnected"
                });
            }
        }

        /// <summary>
        /// Processes incoming RESP data and parses complete messages.
        /// </summary>
        /// <param name="clientId">The client identifier sending the data.</param>
        /// <param name="dataBuffer">The buffer containing received data.</param>
        /// <returns>A task representing the asynchronous processing operation.</returns>
        private async Task ProcessRespData(string clientId, StringBuilder dataBuffer)
        {
            var data = dataBuffer.ToString();
            var lines = data.Split(new[] { "\r\n" }, StringSplitOptions.None);

            int processedChars = 0;

            for (int i = 0; i < lines.Length - 1; i += 2) // Process pairs (command + data)
            {
                if (i + 1 >= lines.Length - 1) break;

                var commandLine = lines[i];
                var dataLine = lines[i + 1];

                if (string.IsNullOrEmpty(commandLine)) continue;

                processedChars += commandLine.Length + dataLine.Length + 4; // +4 for \r\n\r\n

                await ParseAndDispatchRespMessage(clientId, commandLine, dataLine);
            }

            // Remove processed data from buffer
            if (processedChars > 0)
            {
                dataBuffer.Remove(0, processedChars);
            }
        }

        /// <summary>
        /// Parses a RESP message and dispatches the appropriate event.
        /// </summary>
        /// <param name="clientId">The client identifier that sent the message.</param>
        /// <param name="commandLine">The RESP command line containing the type and metadata.</param>
        /// <param name="dataLine">The data line containing the actual payload.</param>
        /// <returns>A task representing the asynchronous parsing operation.</returns>
        private async Task ParseAndDispatchRespMessage(string clientId, string commandLine, string dataLine)
        {
            if (string.IsNullOrEmpty(commandLine)) return;

            var firstChar = commandLine[0];
            var content = commandLine.Length > 1 ? commandLine.Substring(1) : string.Empty;

            var eventArgs = new RespDataReceivedEventArgs
            {
                RawData = $"{commandLine}\r\n{dataLine}"
            };

            try
            {
                switch (firstChar)
                {
                    case '+': // Simple String
                        eventArgs.DataType = RespDataType.SimpleString;
                        eventArgs.Value = content;
                        OnSimpleStringReceived(eventArgs);
                        break;

                    case '-': // Error
                        eventArgs.DataType = RespDataType.Error;
                        eventArgs.Value = content;
                        OnErrorReceived(eventArgs);
                        break;

                    case ':': // Integer
                        eventArgs.DataType = RespDataType.Integer;
                        if (long.TryParse(content, out var intValue))
                        {
                            eventArgs.Value = intValue;
                        }
                        else
                        {
                            eventArgs.Value = content;
                        }
                        OnIntegerReceived(eventArgs);
                        break;

                    case '$': // Bulk String
                        eventArgs.DataType = RespDataType.BulkString;
                        if (int.TryParse(content, out var length))
                        {
                            if (length == -1)
                            {
                                eventArgs.DataType = RespDataType.Null;
                                eventArgs.Value = null;
                                OnNullReceived(eventArgs);
                            }
                            else
                            {
                                eventArgs.Value = dataLine.Length >= length ? dataLine.Substring(0, length) : dataLine;
                                OnBulkStringReceived(eventArgs);
                            }
                        }
                        break;

                    case '*': // Array
                        eventArgs.DataType = RespDataType.Array;
                        if (int.TryParse(content, out var arrayLength))
                        {
                            if (arrayLength == -1)
                            {
                                eventArgs.DataType = RespDataType.Null;
                                eventArgs.Value = null;
                                OnNullReceived(eventArgs);
                            }
                            else
                            {
                                // For simplicity, we'll pass the array length
                                // In a full implementation, you'd parse the entire array
                                eventArgs.Value = new { Length = arrayLength, Data = dataLine };
                                OnArrayReceived(eventArgs);
                            }
                        }
                        break;

                    default:
                        OnErrorOccurred(new ErrorEventArgs
                        {
                            Message = $"Unknown RESP data type: {firstChar}",
                            ClientId = clientId
                        });
                        return;
                }

                OnDataReceived(eventArgs);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Message = "Error parsing RESP message",
                    Exception = ex,
                    ClientId = clientId
                });
            }
        }

        /// <summary>
        /// Raises the <see cref="SimpleStringReceived"/> event.
        /// </summary>
        /// <param name="e">The event arguments containing the simple string data.</param>
        protected virtual void OnSimpleStringReceived(RespDataReceivedEventArgs e) => SimpleStringReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="ErrorReceived"/> event.
        /// </summary>
        /// <param name="e">The event arguments containing the error data.</param>
        protected virtual void OnErrorReceived(RespDataReceivedEventArgs e) => ErrorReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="IntegerReceived"/> event.
        /// </summary>
        /// <param name="e">The event arguments containing the integer data.</param>
        protected virtual void OnIntegerReceived(RespDataReceivedEventArgs e) => IntegerReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="BulkStringReceived"/> event.
        /// </summary>
        /// <param name="e">The event arguments containing the bulk string data.</param>
        protected virtual void OnBulkStringReceived(RespDataReceivedEventArgs e) => BulkStringReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="ArrayReceived"/> event.
        /// </summary>
        /// <param name="e">The event arguments containing the array data.</param>
        protected virtual void OnArrayReceived(RespDataReceivedEventArgs e) => ArrayReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="NullReceived"/> event.
        /// </summary>
        /// <param name="e">The event arguments containing the null value data.</param>
        protected virtual void OnNullReceived(RespDataReceivedEventArgs e) => NullReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="DataReceived"/> event.
        /// </summary>
        /// <param name="e">The event arguments containing the received data.</param>
        protected virtual void OnDataReceived(RespDataReceivedEventArgs e) => DataReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="ClientConnected"/> event.
        /// </summary>
        /// <param name="e">The event arguments containing the client connection information.</param>
        protected virtual void OnClientConnected(ClientConnectedEventArgs e) => ClientConnected?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="ClientDisconnected"/> event.
        /// </summary>
        /// <param name="e">The event arguments containing the client disconnection information.</param>
        protected virtual void OnClientDisconnected(ClientDisconnectedEventArgs e) => ClientDisconnected?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="ErrorOccurred"/> event.
        /// </summary>
        /// <param name="e">The event arguments containing the error information.</param>
        protected virtual void OnErrorOccurred(ErrorEventArgs e) => ErrorOccurred?.Invoke(this, e);

        #endregion

    }
}