namespace RedisResp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// A test server that wraps the RespListener with comprehensive event handlers
    /// for testing and demonstration purposes.
    /// </summary>
    /// <remarks>
    /// This class provides detailed logging and event handling for all RESP protocol events,
    /// making it useful for testing the RespListener functionality and debugging
    /// RESP protocol communications.
    /// </remarks>
    public class TestServer
    {
        #region Public-Members

        /// <summary>
        /// Gets a read-only collection of all messages received by the server.
        /// </summary>
        /// <value>A thread-safe snapshot of all logged messages with timestamps.</value>
        /// <remarks>
        /// This property returns a copy of the internal message list to ensure thread safety.
        /// Messages include timestamps and are formatted for easy reading.
        /// </remarks>
        public IReadOnlyList<string> ReceivedMessages
        {
            get
            {
                lock (_messagesLock)
                {
                    return new List<string>(_receivedMessages);
                }
            }
        }

        #endregion

        #region Private-Members

        private RespListener _listener;
        private readonly List<string> _receivedMessages = new List<string>();
        private readonly object _messagesLock = new object();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="TestServer"/> class.
        /// </summary>
        public TestServer()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Clears all recorded messages from the server log.
        /// </summary>
        /// <remarks>
        /// This method is thread-safe and can be called to reset the message history
        /// between test runs or when starting fresh logging sessions.
        /// </remarks>
        public void ClearMessages()
        {
            lock (_messagesLock)
            {
                _receivedMessages.Clear();
            }
        }

        /// <summary>
        /// Starts the test server on the specified port with comprehensive event handlers.
        /// </summary>
        /// <param name="port">The port number to listen on. Defaults to 6380.</param>
        /// <returns>A task representing the asynchronous start operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the server is already running.</exception>
        /// <exception cref="System.Net.Sockets.SocketException">Thrown if the port is already in use.</exception>
        /// <remarks>
        /// This method creates a new RespListener instance and subscribes to all
        /// available events with detailed logging handlers. The server will log all
        /// RESP protocol interactions with visual indicators and timestamps.
        /// </remarks>
        public async Task StartAsync(int port = 6380)
        {
            _listener = new RespListener(port);

            // Subscribe to all events with detailed handlers
            _listener.SimpleStringReceived += OnSimpleStringReceived;
            _listener.ErrorReceived += OnErrorReceived;
            _listener.IntegerReceived += OnIntegerReceived;
            _listener.BulkStringReceived += OnBulkStringReceived;
            _listener.ArrayReceived += OnArrayReceived;
            _listener.NullReceived += OnNullReceived;
            _listener.ClientConnected += OnClientConnected;
            _listener.ClientDisconnected += OnClientDisconnected;
            _listener.ErrorOccurred += OnErrorOccurred;
            _listener.DataReceived += OnDataReceived;

            await _listener.StartAsync();
            Console.WriteLine($"Test server started on port {port}");
        }

        /// <summary>
        /// Stops the test server and releases all resources.
        /// </summary>
        /// <remarks>
        /// This method gracefully shuts down the underlying RespListener,
        /// disconnects all clients, and disposes of resources.
        /// </remarks>
        public void Stop()
        {
            _listener?.Stop();
            _listener?.Dispose();
            Console.WriteLine("Test server stopped");
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Logs a message with timestamp and adds it to the message collection.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <remarks>
        /// This method is thread-safe and adds timestamps to all logged messages.
        /// Messages are both displayed to the console and stored in the internal collection.
        /// </remarks>
        private void LogMessage(string message)
        {
            var timestampedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Console.WriteLine(timestampedMessage);

            lock (_messagesLock)
            {
                _receivedMessages.Add(timestampedMessage);
            }
        }

        /// <summary>
        /// Handles simple string received events.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments containing the simple string data.</param>
        private void OnSimpleStringReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"📝 SIMPLE STRING: '{e.Value}'");
        }

        /// <summary>
        /// Handles error received events.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments containing the error data.</param>
        private void OnErrorReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"❌ ERROR: '{e.Value}'");
        }

        /// <summary>
        /// Handles integer received events.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments containing the integer data.</param>
        private void OnIntegerReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"🔢 INTEGER: {e.Value}");
        }

        /// <summary>
        /// Handles bulk string received events.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments containing the bulk string data.</param>
        private void OnBulkStringReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"📦 BULK STRING: '{e.Value}' (Length: {e.Value?.ToString()?.Length ?? 0})");
        }

        /// <summary>
        /// Handles array received events.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments containing the array data.</param>
        private void OnArrayReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"📋 ARRAY: {e.Value}");
        }

        /// <summary>
        /// Handles null value received events.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments containing the null value data.</param>
        private void OnNullReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"∅ NULL VALUE");
        }

        /// <summary>
        /// Handles client connected events.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments containing the client connection information.</param>
        private void OnClientConnected(object sender, ClientConnectedEventArgs e)
        {
            LogMessage($"🔗 CLIENT CONNECTED: {e.ClientId} from {e.RemoteEndPoint}");
        }

        /// <summary>
        /// Handles client disconnected events.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments containing the client disconnection information.</param>
        private void OnClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            LogMessage($"🔌 CLIENT DISCONNECTED: {e.ClientId} - {e.Reason}");
        }

        /// <summary>
        /// Handles server error events.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments containing the error information.</param>
        private void OnErrorOccurred(object sender, ErrorEventArgs e)
        {
            LogMessage($"💥 SERVER ERROR: {e.Message} - {e.Exception?.Message ?? "No exception"} (Client: {e.ClientId ?? "Unknown"})");
        }

        /// <summary>
        /// Handles generic data received events for all RESP data types.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments containing the received data.</param>
        private void OnDataReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"📡 RAW DATA [{e.DataType}]: {e.RawData?.Replace("\r\n", "\\r\\n")}");
        }

        #endregion
    }
}