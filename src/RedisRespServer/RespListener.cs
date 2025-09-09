namespace RedisResp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
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
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

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
        /// Raised when a double value is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> DoubleReceived;

        /// <summary>
        /// Raised when a boolean value is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> BooleanReceived;

        /// <summary>
        /// Raised when a big number value is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> BigNumberReceived;

        /// <summary>
        /// Raised when a blob error is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> BlobErrorReceived;

        /// <summary>
        /// Raised when a verbatim string is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> VerbatimStringReceived;

        /// <summary>
        /// Raised when a map is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> MapReceived;

        /// <summary>
        /// Raised when a set is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> SetReceived;

        /// <summary>
        /// Raised when an attribute is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> AttributeReceived;

        /// <summary>
        /// Raised when a push message is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> PushReceived;

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
        /// Gets a value indicating whether the server is currently listening for connections.
        /// </summary>
        /// <value>true if the server is listening; otherwise, false.</value>
        public bool IsListening => _IsListening;

        /// <summary>
        /// Gets the current number of connected clients.
        /// </summary>
        /// <value>The count of active client connections.</value>
        /// <remarks>This property is thread-safe.</remarks>
        public int ConnectedClientsCount
        {
            get
            {
                lock (_ClientsLock)
                {
                    return _Clients.Count;
                }
            }
        }

        /// <summary>
        /// Gets information about all currently connected clients.
        /// </summary>
        /// <returns>An array of ClientInfo objects representing all connected clients.</returns>
        /// <remarks>
        /// This method returns a snapshot of all connected clients at the time of the call.
        /// The returned array is thread-safe and will not be modified by the server.
        /// </remarks>
        public ClientInfo[] RetrieveClients()
        {
            lock (_ClientsLock)
            {
                return _Clients.Values.ToArray();
            }
        }

        /// <summary>
        /// Gets information about a specific client by its GUID.
        /// </summary>
        /// <param name="clientGuid">The GUID of the client to retrieve.</param>
        /// <returns>The ClientInfo object for the specified client, or null if not found.</returns>
        /// <remarks>
        /// This method is thread-safe and returns null if the specified client is not connected.
        /// </remarks>
        public ClientInfo RetrieveClientByGuid(Guid clientGuid)
        {
            lock (_ClientsLock)
            {
                _Clients.TryGetValue(clientGuid, out var clientInfo);
                return clientInfo;
            }
        }

        /// <summary>
        /// Disconnects a specific client by its GUID.
        /// </summary>
        /// <param name="clientGuid">The GUID of the client to disconnect.</param>
        /// <returns>true if the client was found and disconnected; otherwise, false.</returns>
        /// <exception cref="ArgumentException">Thrown when clientGuid is empty.</exception>
        /// <remarks>
        /// This method gracefully disconnects the specified client and removes it from
        /// the connected clients collection. A ClientDisconnected event will be raised.
        /// </remarks>
        public bool DisconnectClientByGuid(Guid clientGuid)
        {
            if (clientGuid == Guid.Empty)
                throw new ArgumentException("Client GUID cannot be empty.", nameof(clientGuid));

            ClientInfo clientInfo;
            lock (_ClientsLock)
            {
                if (!_Clients.TryGetValue(clientGuid, out clientInfo))
                {
                    return false;
                }
                _Clients.Remove(clientGuid);
            }

            try
            {
                clientInfo.TcpClient.Close();
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Message = "Error closing client connection",
                    Exception = ex,
                    GUID = clientGuid
                });
            }

            OnClientDisconnected(new ClientDisconnectedEventArgs
            {
                GUID = clientGuid,
                RemoteEndPoint = clientInfo.RemoteEndPoint,
                Reason = "Disconnected by server"
            });

            return true;
        }

        /// <summary>
        /// Method to invoke to send log messages.
        /// </summary>
        public Action<SeverityEnum, string> Logger { get; set; } = null;

        private int _Port = 6379;
        private TcpListener _TcpListener;
        private bool _IsListening;
        private readonly Dictionary<Guid, ClientInfo> _Clients = new Dictionary<Guid, ClientInfo>();
        private readonly object _ClientsLock = new object();

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
            if (port < 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            _Port = port;
            _TcpListener = new TcpListener(IPAddress.Any, port);
        }

        /// <summary>
        /// Starts the server and begins listening for client connections asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous start operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the server is already listening.</exception>
        /// <exception cref="SocketException">Thrown if the port is already in use or other socket errors occur.</exception>
        /// <remarks>
        /// This method starts the TCP listener and begins accepting client connections in the background.
        /// The method returns immediately after starting the listener; client connections are handled asynchronously.
        /// </remarks>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_IsListening) return;

            _TcpListener.Start();
            _IsListening = true;

            Log(SeverityEnum.Info, $"redis listener started on port {_Port}");

            _ = Task.Run(() => AcceptClientsAsync(cancellationToken));
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
            if (!_IsListening) return;

            _IsListening = false;
            _TcpListener?.Stop();

            // Disconnect all clients
            lock (_ClientsLock)
            {
                foreach (KeyValuePair<Guid, ClientInfo> kvp in _Clients)
                {
                    kvp.Value.TcpClient.Close();
                    OnClientDisconnected(new ClientDisconnectedEventArgs
                    {
                        GUID = kvp.Key,
                        RemoteEndPoint = kvp.Value.RemoteEndPoint,
                        Reason = "Server shutdown"
                    });
                }
                _Clients.Clear();
            }

            Log(SeverityEnum.Info, "redis listener stopped");
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
            _TcpListener?.Stop();
        }

        /// <summary>
        /// Raises the <see cref="SimpleStringReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the simple string information.</param>
        protected virtual void OnSimpleStringReceived(RespDataReceivedEventArgs e) => SimpleStringReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="ErrorReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the error information.</param>
        protected virtual void OnErrorReceived(RespDataReceivedEventArgs e) => ErrorReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="IntegerReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the integer information.</param>
        protected virtual void OnIntegerReceived(RespDataReceivedEventArgs e) => IntegerReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="BulkStringReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the bulk string information.</param>
        protected virtual void OnBulkStringReceived(RespDataReceivedEventArgs e) => BulkStringReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="ArrayReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the array information.</param>
        protected virtual void OnArrayReceived(RespDataReceivedEventArgs e) => ArrayReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="NullReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the null value information.</param>
        protected virtual void OnNullReceived(RespDataReceivedEventArgs e) => NullReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="DoubleReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the double value information.</param>
        protected virtual void OnDoubleReceived(RespDataReceivedEventArgs e) => DoubleReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="BooleanReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the boolean value information.</param>
        protected virtual void OnBooleanReceived(RespDataReceivedEventArgs e) => BooleanReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="BigNumberReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the big number value information.</param>
        protected virtual void OnBigNumberReceived(RespDataReceivedEventArgs e) => BigNumberReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="BlobErrorReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the blob error information.</param>
        protected virtual void OnBlobErrorReceived(RespDataReceivedEventArgs e) => BlobErrorReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="VerbatimStringReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the verbatim string information.</param>
        protected virtual void OnVerbatimStringReceived(RespDataReceivedEventArgs e) => VerbatimStringReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="MapReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the map information.</param>
        protected virtual void OnMapReceived(RespDataReceivedEventArgs e) => MapReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="SetReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the set information.</param>
        protected virtual void OnSetReceived(RespDataReceivedEventArgs e) => SetReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="AttributeReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the attribute information.</param>
        protected virtual void OnAttributeReceived(RespDataReceivedEventArgs e) => AttributeReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="PushReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the push message information.</param>
        protected virtual void OnPushReceived(RespDataReceivedEventArgs e) => PushReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="DataReceived"/> event.
        /// </summary>
        /// <param name="e">The event data containing the received data information.</param>
        protected virtual void OnDataReceived(RespDataReceivedEventArgs e) => DataReceived?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="ClientConnected"/> event.
        /// </summary>
        /// <param name="e">The event data containing the client connection information.</param>
        protected virtual void OnClientConnected(ClientConnectedEventArgs e) => ClientConnected?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="ClientDisconnected"/> event.
        /// </summary>
        /// <param name="e">The event data containing the client disconnection information.</param>
        protected virtual void OnClientDisconnected(ClientDisconnectedEventArgs e) => ClientDisconnected?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="ErrorOccurred"/> event.
        /// </summary>
        /// <param name="e">The event data containing the error information.</param>
        protected virtual void OnErrorOccurred(ErrorEventArgs e) => ErrorOccurred?.Invoke(this, e);

        private void Log(SeverityEnum sev, string msg)
        {
            Logger?.Invoke(sev, msg);
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken = default)
        {
            while (_IsListening)
            {
                try
                {
                    TcpClient tcpClient = await _TcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                    Guid clientGuid = Guid.NewGuid();
                    IPEndPoint remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
                    ClientInfo clientInfo = new ClientInfo(clientGuid, tcpClient, remoteEndPoint);

                    lock (_ClientsLock)
                    {
                        _Clients[clientGuid] = clientInfo;
                    }

                    OnClientConnected(new ClientConnectedEventArgs
                    {
                        GUID = clientGuid,
                        RemoteEndPoint = remoteEndPoint
                    });

                    // Handle client in separate task
                    _ = Task.Run(() => HandleClientAsync(clientGuid, clientInfo, cancellationToken));
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

        private async Task HandleClientAsync(Guid clientGuid, ClientInfo clientInfo, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[4096];
            StringBuilder dataBuffer = new StringBuilder();
            List<byte> rawByteBuffer = new List<byte>(); // Preserve raw bytes

            try
            {
                using (clientInfo.TcpClient)
                using (NetworkStream stream = clientInfo.TcpClient.GetStream())
                {
                    while (clientInfo.TcpClient.Connected && _IsListening)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                        if (bytesRead == 0) break;

                        // Store raw bytes
                        for (int i = 0; i < bytesRead; i++)
                        {
                            rawByteBuffer.Add(buffer[i]);
                        }

                        // Use Latin1 encoding which preserves all byte values 1:1, preventing binary data corruption
                        // This is crucial for ECHO commands that may contain binary data from clients like StackExchange.Redis
                        string data = Encoding.Latin1.GetString(buffer, 0, bytesRead);
                        dataBuffer.Append(data);

                        // Process complete RESP messages
                        await ProcessRespData(clientGuid, dataBuffer, rawByteBuffer, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (IOException ioEx) when (ioEx.InnerException is SocketException)
            {
                // Client disconnected - this is normal, don't log as an error
                Log(SeverityEnum.Warn, $"client {clientGuid} disconnected");
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Message = "Error handling client",
                    Exception = ex,
                    GUID = clientGuid
                });
            }
            finally
            {
                lock (_ClientsLock)
                {
                    _Clients.Remove(clientGuid);
                }

                OnClientDisconnected(new ClientDisconnectedEventArgs
                {
                    GUID = clientGuid,
                    RemoteEndPoint = clientInfo.RemoteEndPoint,
                    Reason = "Client disconnected"
                });
            }
        }

        private async Task ProcessRespData(Guid clientGuid, StringBuilder dataBuffer, List<byte> rawByteBuffer, CancellationToken cancellationToken = default)
        {
            var data = dataBuffer.ToString();
            int processedChars = 0;
            int processedBytes = 0;

            while (processedChars < data.Length)
            {
                var remainingData = data.Substring(processedChars);
                RespParseResult parseResult = TryParseRespMessageWithBytes(remainingData, rawByteBuffer, processedBytes);

                if (parseResult == null)
                {
                    // Not enough data to parse complete message yet
                    break;
                }

                // Extract message bytes for this specific message
                byte[] messageBytes = new byte[parseResult.BytesConsumed];
                for (int i = 0; i < parseResult.BytesConsumed; i++)
                {
                    if (processedBytes + i < rawByteBuffer.Count)
                    {
                        messageBytes[i] = rawByteBuffer[processedBytes + i];
                    }
                }

                await DispatchRespMessageWithBytes(clientGuid, parseResult.Message, parseResult.RawData, parseResult.RawBytes, messageBytes, cancellationToken).ConfigureAwait(false);
                processedChars += parseResult.BytesConsumed;
                processedBytes += parseResult.BytesConsumed;
            }

            // Remove processed data from buffers
            if (processedChars > 0)
            {
                dataBuffer.Remove(0, processedChars);
            }
            if (processedBytes > 0)
            {
                rawByteBuffer.RemoveRange(0, processedBytes);
            }
        }

        private RespParseResult TryParseRespMessageWithBytes(string data, List<byte> rawByteBuffer, int byteOffset)
        {
            if (string.IsNullOrEmpty(data)) return null;

            char firstChar = data[0];
            int crlfIndex = data.IndexOf("\r\n");
            if (crlfIndex == -1) return null; // Not enough data

            string content = data.Substring(1, crlfIndex - 1);

            switch (firstChar)
            {
                case '+': // Simple String
                    return new RespParseResult(content, data.Substring(0, crlfIndex + 2), crlfIndex + 2);

                case '-': // Error
                    return new RespParseResult(content, data.Substring(0, crlfIndex + 2), crlfIndex + 2);

                case ':': // Integer
                    long parsedInt;
                    object intValue = long.TryParse(content, out parsedInt) ? parsedInt : (object)content;
                    return new RespParseResult(intValue, data.Substring(0, crlfIndex + 2), crlfIndex + 2);

                case '$': // Bulk String - PRESERVE RAW BYTES
                    if (!int.TryParse(content, out var length)) return null;

                    if (length == -1)
                    {
                        return new RespParseResult(null, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }

                    int expectedEndIndex = crlfIndex + 2 + length + 2; // command + length + data + \r\n
                    if (data.Length < expectedEndIndex) return null; // Not enough data

                    // Extract raw bytes for bulk string data
                    int bulkDataStartByte = byteOffset + crlfIndex + 2;
                    byte[] bulkDataBytes = new byte[length];
                    if (bulkDataStartByte + length <= rawByteBuffer.Count)
                    {
                        for (int i = 0; i < length; i++)
                        {
                            bulkDataBytes[i] = rawByteBuffer[bulkDataStartByte + i];
                        }
                    }

                    string bulkData = data.Substring(crlfIndex + 2, length);
                    return new RespParseResult(bulkData, data.Substring(0, expectedEndIndex), expectedEndIndex, bulkDataBytes);

                case '*': // Array
                    if (!int.TryParse(content, out var arrayLength)) return null;

                    if (arrayLength == -1)
                    {
                        return new RespParseResult(null, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }

                    List<object> elements = new List<object>();
                    List<byte[]> elementBytes = new List<byte[]>(); // Track raw bytes for each element
                    int consumed = crlfIndex + 2;
                    int consumedBytes = byteOffset + consumed;

                    for (int i = 0; i < arrayLength; i++)
                    {
                        string remainingData = data.Substring(consumed);
                        RespParseResult elementResult = TryParseRespMessageWithBytes(remainingData, rawByteBuffer, consumedBytes);

                        if (elementResult == null) return null; // Not enough data

                        elements.Add(elementResult.Message);
                        elementBytes.Add(elementResult.RawBytes);
                        consumed += elementResult.BytesConsumed;
                        consumedBytes += elementResult.BytesConsumed;
                    }

                    // Store element bytes in a way that can be retrieved
                    return new RespParseResult(elements.ToArray(), data.Substring(0, consumed), consumed);

                case ',': // Double (RESP3)
                    if (double.TryParse(content, out double doubleValue))
                    {
                        return new RespParseResult(doubleValue, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }
                    return new RespParseResult(content, data.Substring(0, crlfIndex + 2), crlfIndex + 2);

                case '#': // Boolean (RESP3)
                    if (content == "t")
                    {
                        return new RespParseResult(true, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }
                    else if (content == "f")
                    {
                        return new RespParseResult(false, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }
                    return new RespParseResult(content, data.Substring(0, crlfIndex + 2), crlfIndex + 2);

                case '(': // BigNumber (RESP3)
                    return new RespParseResult(content, data.Substring(0, crlfIndex + 2), crlfIndex + 2);

                case '!': // BlobError (RESP3) - similar to bulk string but represents an error
                    if (!int.TryParse(content, out var errorLength)) return null;

                    if (errorLength == -1)
                    {
                        return new RespParseResult(null, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }

                    int expectedErrorEndIndex = crlfIndex + 2 + errorLength + 2;
                    if (data.Length < expectedErrorEndIndex) return null;

                    // Extract raw bytes for blob error data
                    int errorDataStartByte = byteOffset + crlfIndex + 2;
                    byte[] errorDataBytes = new byte[errorLength];
                    if (errorDataStartByte + errorLength <= rawByteBuffer.Count)
                    {
                        for (int i = 0; i < errorLength; i++)
                        {
                            errorDataBytes[i] = rawByteBuffer[errorDataStartByte + i];
                        }
                    }

                    string errorData = data.Substring(crlfIndex + 2, errorLength);
                    return new RespParseResult(new { Type = "BlobError", Message = errorData }, data.Substring(0, expectedErrorEndIndex), expectedErrorEndIndex, errorDataBytes);

                case '=': // VerbatimString (RESP3) - similar to bulk string with encoding prefix
                    if (!int.TryParse(content, out var verbatimLength)) return null;

                    if (verbatimLength == -1)
                    {
                        return new RespParseResult(null, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }

                    int expectedVerbatimEndIndex = crlfIndex + 2 + verbatimLength + 2;
                    if (data.Length < expectedVerbatimEndIndex) return null;

                    // Extract raw bytes for verbatim string data
                    int verbatimDataStartByte = byteOffset + crlfIndex + 2;
                    byte[] verbatimDataBytes = new byte[verbatimLength];
                    if (verbatimDataStartByte + verbatimLength <= rawByteBuffer.Count)
                    {
                        for (int i = 0; i < verbatimLength; i++)
                        {
                            verbatimDataBytes[i] = rawByteBuffer[verbatimDataStartByte + i];
                        }
                    }

                    string verbatimData = data.Substring(crlfIndex + 2, verbatimLength);
                    return new RespParseResult(verbatimData, data.Substring(0, expectedVerbatimEndIndex), expectedVerbatimEndIndex, verbatimDataBytes);

                case '%': // Map (RESP3) - similar to array but key-value pairs
                    if (!int.TryParse(content, out var mapLength)) return null;

                    if (mapLength == -1)
                    {
                        return new RespParseResult(null, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }

                    List<object> mapElements = new List<object>();
                    int mapConsumed = crlfIndex + 2;
                    int mapConsumedBytes = byteOffset + mapConsumed;

                    // Map has mapLength * 2 elements (key-value pairs)
                    for (int i = 0; i < mapLength * 2; i++)
                    {
                        string remainingMapData = data.Substring(mapConsumed);
                        RespParseResult mapElementResult = TryParseRespMessageWithBytes(remainingMapData, rawByteBuffer, mapConsumedBytes);

                        if (mapElementResult == null) return null;

                        mapElements.Add(mapElementResult.Message);
                        mapConsumed += mapElementResult.BytesConsumed;
                        mapConsumedBytes += mapElementResult.BytesConsumed;
                    }

                    return new RespParseResult(mapElements.ToArray(), data.Substring(0, mapConsumed), mapConsumed);

                case '~': // Set (RESP3) - similar to array
                    if (!int.TryParse(content, out var setLength)) return null;

                    if (setLength == -1)
                    {
                        return new RespParseResult(null, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }

                    List<object> setElements = new List<object>();
                    int setConsumed = crlfIndex + 2;
                    int setConsumedBytes = byteOffset + setConsumed;

                    for (int i = 0; i < setLength; i++)
                    {
                        string remainingSetData = data.Substring(setConsumed);
                        RespParseResult setElementResult = TryParseRespMessageWithBytes(remainingSetData, rawByteBuffer, setConsumedBytes);

                        if (setElementResult == null) return null;

                        setElements.Add(setElementResult.Message);
                        setConsumed += setElementResult.BytesConsumed;
                        setConsumedBytes += setElementResult.BytesConsumed;
                    }

                    return new RespParseResult(setElements.ToArray(), data.Substring(0, setConsumed), setConsumed);

                case '|': // Attribute (RESP3) - similar to map but metadata
                    if (!int.TryParse(content, out var attributeLength)) return null;

                    if (attributeLength == -1)
                    {
                        return new RespParseResult(null, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }

                    List<object> attributeElements = new List<object>();
                    int attributeConsumed = crlfIndex + 2;
                    int attributeConsumedBytes = byteOffset + attributeConsumed;

                    // Attribute has attributeLength * 2 elements (key-value pairs)
                    for (int i = 0; i < attributeLength * 2; i++)
                    {
                        string remainingAttributeData = data.Substring(attributeConsumed);
                        RespParseResult attributeElementResult = TryParseRespMessageWithBytes(remainingAttributeData, rawByteBuffer, attributeConsumedBytes);

                        if (attributeElementResult == null) return null;

                        attributeElements.Add(attributeElementResult.Message);
                        attributeConsumed += attributeElementResult.BytesConsumed;
                        attributeConsumedBytes += attributeElementResult.BytesConsumed;
                    }

                    return new RespParseResult(attributeElements.ToArray(), data.Substring(0, attributeConsumed), attributeConsumed);

                case '>': // Push (RESP3) - similar to array
                    if (!int.TryParse(content, out var pushLength)) return null;

                    if (pushLength == -1)
                    {
                        return new RespParseResult(null, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }

                    List<object> pushElements = new List<object>();
                    int pushConsumed = crlfIndex + 2;
                    int pushConsumedBytes = byteOffset + pushConsumed;

                    for (int i = 0; i < pushLength; i++)
                    {
                        string remainingPushData = data.Substring(pushConsumed);
                        RespParseResult pushElementResult = TryParseRespMessageWithBytes(remainingPushData, rawByteBuffer, pushConsumedBytes);

                        if (pushElementResult == null) return null;

                        pushElements.Add(pushElementResult.Message);
                        pushConsumed += pushElementResult.BytesConsumed;
                        pushConsumedBytes += pushElementResult.BytesConsumed;
                    }

                    return new RespParseResult(pushElements.ToArray(), data.Substring(0, pushConsumed), pushConsumed);

                default:
                    return null;
            }
        }

        private RespParseResult TryParseRespMessage(string data)
        {
            if (string.IsNullOrEmpty(data)) return null;

            char firstChar = data[0];
            int crlfIndex = data.IndexOf("\r\n");
            if (crlfIndex == -1) return null; // Not enough data

            string content = data.Substring(1, crlfIndex - 1);

            switch (firstChar)
            {
                case '+': // Simple String
                    return new RespParseResult(content, data.Substring(0, crlfIndex + 2), crlfIndex + 2);

                case '-': // Error
                    return new RespParseResult(new { Type = "Error", Message = content }, data.Substring(0, crlfIndex + 2), crlfIndex + 2);

                case ':': // Integer
                    long parsedInt;
                    object intValue = long.TryParse(content, out parsedInt) ? parsedInt : (object)content;
                    return new RespParseResult(intValue, data.Substring(0, crlfIndex + 2), crlfIndex + 2);

                case '$': // Bulk String
                    if (!int.TryParse(content, out var length)) return null;

                    if (length == -1)
                    {
                        return new RespParseResult(null, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }

                    int expectedEndIndex = crlfIndex + 2 + length + 2; // command + length + data + \r\n
                    if (data.Length < expectedEndIndex) return null; // Not enough data

                    string bulkData = data.Substring(crlfIndex + 2, length);
                    return new RespParseResult(bulkData, data.Substring(0, expectedEndIndex), expectedEndIndex);

                case '*': // Array
                    if (!int.TryParse(content, out var arrayLength)) return null;

                    if (arrayLength == -1)
                    {
                        return new RespParseResult(null, data.Substring(0, crlfIndex + 2), crlfIndex + 2);
                    }

                    List<object> elements = new List<object>();
                    int consumed = crlfIndex + 2;

                    for (int i = 0; i < arrayLength; i++)
                    {
                        string remainingData = data.Substring(consumed);
                        RespParseResult elementResult = TryParseRespMessage(remainingData);

                        if (elementResult == null) return null; // Not enough data

                        elements.Add(elementResult.Message);
                        consumed += elementResult.BytesConsumed;
                    }

                    return new RespParseResult(elements.ToArray(), data.Substring(0, consumed), consumed);

                default:
                    return null;
            }
        }

        private async Task DispatchRespMessageWithBytes(Guid clientGuid, object message, string rawData, byte[] rawBytes, byte[] messageBytes, CancellationToken cancellationToken = default)
        {
            RespDataReceivedEventArgs eventArgs = new RespDataReceivedEventArgs
            {
                RawData = rawData,
                ClientGUID = clientGuid,
                RawBytes = rawBytes,
                MessageBytes = messageBytes
            };

            try
            {
                if (message == null)
                {
                    eventArgs.DataType = RespDataType.Null;
                    eventArgs.ProtocolVersion = RespVersionEnum.RESP2;
                    eventArgs.Value = null;
                    OnNullReceived(eventArgs);
                }
                else if (message is string stringMessage)
                {
                    if (rawData.StartsWith("+"))
                    {
                        eventArgs.DataType = RespDataType.SimpleString;
                        eventArgs.ProtocolVersion = RespVersionEnum.RESP2;
                        eventArgs.Value = stringMessage;
                        OnSimpleStringReceived(eventArgs);
                    }
                    else if (rawData.StartsWith("-"))
                    {
                        eventArgs.DataType = RespDataType.Error;
                        eventArgs.ProtocolVersion = RespVersionEnum.RESP2;
                        eventArgs.Value = stringMessage;
                        OnErrorReceived(eventArgs);
                    }
                    else if (rawData.StartsWith("$"))
                    {
                        eventArgs.DataType = RespDataType.BulkString;
                        eventArgs.ProtocolVersion = RespVersionEnum.RESP2;
                        eventArgs.Value = stringMessage;
                        OnBulkStringReceived(eventArgs);
                    }
                    else if (rawData.StartsWith("="))
                    {
                        eventArgs.DataType = RespDataType.VerbatimString;
                        eventArgs.ProtocolVersion = RespVersionEnum.RESP3;
                        eventArgs.Value = stringMessage;
                        OnVerbatimStringReceived(eventArgs);
                    }
                    else if (rawData.StartsWith("("))
                    {
                        eventArgs.DataType = RespDataType.BigNumber;
                        eventArgs.ProtocolVersion = RespVersionEnum.RESP3;
                        eventArgs.Value = stringMessage;
                        OnBigNumberReceived(eventArgs);
                    }
                }
                else if (message is double doubleMessage)
                {
                    eventArgs.DataType = RespDataType.Double;
                    eventArgs.ProtocolVersion = RespVersionEnum.RESP3;
                    eventArgs.Value = doubleMessage;
                    OnDoubleReceived(eventArgs);
                }
                else if (message is bool boolMessage)
                {
                    eventArgs.DataType = RespDataType.Boolean;
                    eventArgs.ProtocolVersion = RespVersionEnum.RESP3;
                    eventArgs.Value = boolMessage;
                    OnBooleanReceived(eventArgs);
                }
                else if (message is long || (message is string && long.TryParse((string)message, out _)))
                {
                    eventArgs.DataType = RespDataType.Integer;
                    eventArgs.ProtocolVersion = RespVersionEnum.RESP2;
                    eventArgs.Value = message;
                    OnIntegerReceived(eventArgs);
                }
                else if (message is object[] arrayData)
                {
                    if (rawData.StartsWith("*"))
                    {
                        eventArgs.DataType = RespDataType.Array;
                        eventArgs.ProtocolVersion = RespVersionEnum.RESP2;
                        eventArgs.Value = arrayData;
                        OnArrayReceived(eventArgs);
                    }
                    else if (rawData.StartsWith("%"))
                    {
                        eventArgs.DataType = RespDataType.Map;
                        eventArgs.ProtocolVersion = RespVersionEnum.RESP3;
                        eventArgs.Value = arrayData;
                        OnMapReceived(eventArgs);
                    }
                    else if (rawData.StartsWith("~"))
                    {
                        eventArgs.DataType = RespDataType.Set;
                        eventArgs.ProtocolVersion = RespVersionEnum.RESP3;
                        eventArgs.Value = arrayData;
                        OnSetReceived(eventArgs);
                    }
                    else if (rawData.StartsWith("|"))
                    {
                        eventArgs.DataType = RespDataType.Attribute;
                        eventArgs.ProtocolVersion = RespVersionEnum.RESP3;
                        eventArgs.Value = arrayData;
                        OnAttributeReceived(eventArgs);
                    }
                    else if (rawData.StartsWith(">"))
                    {
                        eventArgs.DataType = RespDataType.Push;
                        eventArgs.ProtocolVersion = RespVersionEnum.RESP3;
                        eventArgs.Value = arrayData;
                        OnPushReceived(eventArgs);
                    }
                    else
                    {
                        eventArgs.DataType = RespDataType.Array;
                        eventArgs.ProtocolVersion = RespVersionEnum.RESP2;
                        eventArgs.Value = arrayData;
                        OnArrayReceived(eventArgs);
                    }
                }
                else if (message != null && message.GetType().Name.Contains("Type") && message.ToString().Contains("BlobError"))
                {
                    eventArgs.DataType = RespDataType.BlobError;
                    eventArgs.ProtocolVersion = RespVersionEnum.RESP3;
                    eventArgs.Value = message;
                    OnBlobErrorReceived(eventArgs);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Message = "Error dispatching RESP message",
                    Exception = ex
                });
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}