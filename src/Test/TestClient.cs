namespace RedisResp.Tests
{
    using System;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// A test client for sending RESP protocol data to a Redis Protocol Listener server.
    /// </summary>
    /// <remarks>
    /// This class provides a simple TCP client interface for testing RESP protocol communication.
    /// It can send raw RESP data strings and handles connection management for testing purposes.
    /// Includes methods for simulating sudden disconnections to test server resilience.
    /// </remarks>
    public class TestClient
    {
#pragma warning disable CS8602 // Dereference of a possibly null reference.

        /// <summary>
        /// Gets a value indicating whether the client is currently connected to the server.
        /// </summary>
        /// <value>true if the client is connected; otherwise, false.</value>
        public bool IsConnected => _TcpClient?.Connected ?? false;

        /// <summary>
        /// Gets the host address the client is connected to.
        /// </summary>
        /// <value>The hostname or IP address of the server.</value>
        public string Host => _Host;

        /// <summary>
        /// Gets the port number the client is connected to.
        /// </summary>
        /// <value>The TCP port number of the server.</value>
        public int Port => _Port;

        private TcpClient? _TcpClient;
        private NetworkStream? _Stream;
        private string _Host = string.Empty;
        private int _Port;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestClient"/> class.
        /// </summary>
        public TestClient()
        {
        }

        /// <summary>
        /// Connects to the specified Redis Protocol Listener server asynchronously.
        /// </summary>
        /// <param name="host">The hostname or IP address of the server. Defaults to "localhost".</param>
        /// <param name="port">The port number of the server. Defaults to 6380.</param>
        /// <returns>
        /// A task that represents the asynchronous connect operation. 
        /// The task result is true if the connection was successful; otherwise, false.
        /// </returns>
        /// <remarks>
        /// This method establishes a TCP connection to the server and prepares the network stream
        /// for sending RESP protocol data. Connection failures are logged but not thrown as exceptions.
        /// </remarks>
        public async Task<bool> ConnectAsync(string host = "localhost", int port = 6380, CancellationToken cancellationToken = default)
        {
            try
            {
                _Host = host;
                _Port = port;
                _TcpClient = new TcpClient();
                await _TcpClient.ConnectAsync(host, port).ConfigureAwait(false);
                _Stream = _TcpClient.GetStream();
                Console.WriteLine($"[CONN] Test client connected to {host}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to connect: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sends raw RESP protocol data to the connected server asynchronously.
        /// </summary>
        /// <param name="data">The raw RESP protocol string to send, including proper line endings (\r\n).</param>
        /// <returns>
        /// A task that represents the asynchronous send operation.
        /// The task result is true if the data was sent successfully; otherwise, false.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the client is not connected to a server.</exception>
        /// <remarks>
        /// The data parameter should contain properly formatted RESP protocol strings with 
        /// appropriate line endings (\r\n). The method will send the data as UTF-8 encoded bytes
        /// and provide console output for monitoring the sent data.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Send a simple string
        /// await client.SendRespDataAsync("+OK\r\n");
        /// 
        /// // Send a bulk string
        /// await client.SendRespDataAsync("$6\r\nfoobar\r\n");
        /// 
        /// // Send an integer
        /// await client.SendRespDataAsync(":1000\r\n");
        /// </code>
        /// </example>
        public async Task<bool> SendRespDataAsync(string data, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                Console.WriteLine("[ERROR] Not connected to server");
                return false;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                await _Stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                await _Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                Console.WriteLine($"[SENT] {data.Replace("\r\n", "\\r\\n")}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to send data: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Simulates a sudden client disconnection without proper shutdown.
        /// </summary>
        /// <remarks>
        /// This method forcibly closes the underlying TCP connection without sending
        /// a proper disconnect signal to the server. This is useful for testing
        /// server resilience to unexpected client disconnections.
        /// </remarks>
        public void SimulateSuddenDisconnection()
        {
            try
            {
                if (_TcpClient?.Client != null)
                {
                    // Force close the socket without proper shutdown
                    _TcpClient.Client.Close(0);
                }
                Console.WriteLine("[DISCONNECT] Simulated sudden disconnection");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error during sudden disconnection: {ex.Message}");
            }
            finally
            {
                CleanupResources();
            }
        }

        /// <summary>
        /// Disconnects from the server and releases network resources.
        /// </summary>
        /// <remarks>
        /// This method gracefully closes the network stream and TCP connection.
        /// It can be called multiple times safely and will handle any cleanup errors gracefully.
        /// After calling this method, the client must reconnect before sending more data.
        /// </remarks>
        public void Disconnect()
        {
            try
            {
                _Stream?.Close();
                _TcpClient?.Close();
                Console.WriteLine("[DISCONNECT] Test client disconnected");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error disconnecting: {ex.Message}");
            }
            finally
            {
                CleanupResources();
            }
        }

        /// <summary>
        /// Cleans up internal resources and resets connection state.
        /// </summary>
        private void CleanupResources()
        {
            _Stream?.Dispose();
            _TcpClient?.Dispose();
            _Stream = null;
            _TcpClient = null;
        }

#pragma warning restore CS8602 // Dereference of a possibly null reference.
    }
}