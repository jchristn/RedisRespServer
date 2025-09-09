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
#pragma warning disable CS8618
#pragma warning disable CS8622 

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
                lock (_MessagesLock)
                {
                    return new List<string>(_Messages);
                }
            }
        }

        private RespListener _RespListener;
        private readonly List<string> _Messages = new List<string>();
        private readonly object _MessagesLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="TestServer"/> class.
        /// </summary>
        public TestServer()
        {
        }

        /// <summary>
        /// Clears all recorded messages from the server log.
        /// </summary>
        /// <remarks>
        /// This method is thread-safe and can be called to reset the message history
        /// between test runs or when starting fresh logging sessions.
        /// </remarks>
        public void ClearMessages()
        {
            lock (_MessagesLock)
            {
                _Messages.Clear();
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
        public async Task StartAsync(int port = 6379, CancellationToken cancellationToken = default)
        {
            _RespListener = new RespListener(port);

            _RespListener.SimpleStringReceived += OnSimpleStringReceived;
            _RespListener.ErrorReceived += OnErrorReceived;
            _RespListener.IntegerReceived += OnIntegerReceived;
            _RespListener.BulkStringReceived += OnBulkStringReceived;
            _RespListener.ArrayReceived += OnArrayReceived;
            _RespListener.NullReceived += OnNullReceived;
            _RespListener.ClientConnected += OnClientConnected;
            _RespListener.ClientDisconnected += OnClientDisconnected;
            _RespListener.ErrorOccurred += OnErrorOccurred;
            _RespListener.DataReceived += OnDataReceived;

            await _RespListener.StartAsync();
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
            _RespListener?.Stop();
            _RespListener?.Dispose();
            Console.WriteLine("Test server stopped");
        }

        private void LogMessage(string message)
        {
            var timestampedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Console.WriteLine(timestampedMessage);

            lock (_MessagesLock)
            {
                _Messages.Add(timestampedMessage);
            }
        }

        private void OnSimpleStringReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"📝 SIMPLE STRING: '{e.Value}'");
        }

        private void OnErrorReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"❌ ERROR: '{e.Value}'");
        }

        private void OnIntegerReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"🔢 INTEGER: {e.Value}");
        }

        private void OnBulkStringReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"📦 BULK STRING: '{e.Value}' (Length: {e.Value?.ToString()?.Length ?? 0})");
        }

        private void OnArrayReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"📋 ARRAY: {e.Value}");
        }

        private void OnNullReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"∅ NULL VALUE");
        }

        private void OnClientConnected(object sender, ClientConnectedEventArgs e)
        {
            LogMessage($"🔗 CLIENT CONNECTED: {e.GUID} from {e.RemoteEndPoint}");
        }

        private void OnClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            LogMessage($"🔌 CLIENT DISCONNECTED: {e.GUID} - {e.Reason}");
        }

        private void OnErrorOccurred(object sender, ErrorEventArgs e)
        {
            LogMessage($"💥 SERVER ERROR: {e.Message} - {e.Exception?.Message ?? "No exception"} (Client: {e.GUID?.ToString() ?? "Unknown"})");
        }

        private void OnDataReceived(object sender, RespDataReceivedEventArgs e)
        {
            LogMessage($"📡 RAW DATA [{e.DataType}]: {e.RawData?.Replace("\r\n", "\\r\\n")}");
        }

#pragma warning restore CS8618
#pragma warning restore CS8622
    }
}