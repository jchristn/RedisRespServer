namespace Test.Shared.Infrastructure
{
    using System;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A minimal raw TCP client used to send arbitrary bytes to a listener and read responses.
    /// </summary>
    /// <remarks>
    /// This client intentionally does not understand the RESP protocol. It sends exactly the
    /// bytes it is given and reads whatever bytes come back, which lets tests exercise the
    /// server's parser with precisely controlled (including malformed or fragmented) input.
    /// The Latin1 encoding is used so that every byte value round-trips 1:1.
    /// </remarks>
    public sealed class RespRawClient : IDisposable
    {

        #region Public-Members

        /// <summary>
        /// Gets a value indicating whether the client is currently connected.
        /// </summary>
        /// <value>True if the underlying socket reports a connection; otherwise, false.</value>
        public bool IsConnected
        {
            get { return _TcpClient != null && _TcpClient.Connected; }
        }

        #endregion


        #region Private-Members

        private TcpClient _TcpClient;
        private NetworkStream _Stream;

        #endregion


        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="RespRawClient"/> class.
        /// </summary>
        public RespRawClient()
        {
        }

        #endregion


        #region Public-Methods

        /// <summary>
        /// Connects to the specified host and port.
        /// </summary>
        /// <param name="host">The host name or IP address.</param>
        /// <param name="port">The TCP port.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task that completes when the connection is established.</returns>
        public async Task ConnectAsync(string host, int port, CancellationToken token = default)
        {
            _TcpClient = new TcpClient();
            await _TcpClient.ConnectAsync(host, port).ConfigureAwait(false);
            _Stream = _TcpClient.GetStream();
        }

        /// <summary>
        /// Sends a string as raw bytes using Latin1 encoding.
        /// </summary>
        /// <param name="data">The exact string to send.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task that completes when the bytes have been flushed.</returns>
        public async Task SendAsync(string data, CancellationToken token = default)
        {
            byte[] bytes = Encoding.Latin1.GetBytes(data);
            await _Stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            await _Stream.FlushAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends raw bytes to the server.
        /// </summary>
        /// <param name="bytes">The bytes to send.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A task that completes when the bytes have been flushed.</returns>
        public async Task SendBytesAsync(byte[] bytes, CancellationToken token = default)
        {
            await _Stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            await _Stream.FlushAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a single line terminated by CRLF from the server.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for a complete line.</param>
        /// <returns>The line contents without the trailing CRLF, or null if the connection closed.</returns>
        public async Task<string> ReadLineAsync(TimeSpan timeout)
        {
            StringBuilder sb = new StringBuilder();
            byte[] one = new byte[1];
            DateTime deadline = DateTime.UtcNow.Add(timeout);

            while (DateTime.UtcNow < deadline)
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(timeout))
                {
                    int read;
                    try
                    {
                        read = await _Stream.ReadAsync(one, 0, 1, cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }

                    if (read == 0) return sb.Length == 0 ? null : sb.ToString();

                    char c = (char)one[0];
                    if (c == '\r') continue;
                    if (c == '\n') return sb.ToString();
                    sb.Append(c);
                }
            }

            return sb.Length == 0 ? null : sb.ToString();
        }

        /// <summary>
        /// Reads up to the specified number of bytes from the server, or until the timeout elapses.
        /// </summary>
        /// <param name="timeout">Maximum time to wait for data.</param>
        /// <returns>The accumulated response decoded with Latin1.</returns>
        public async Task<string> ReadAvailableAsync(TimeSpan timeout)
        {
            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[4096];

            using (CancellationTokenSource cts = new CancellationTokenSource(timeout))
            {
                try
                {
                    int read = await _Stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                    if (read > 0) sb.Append(Encoding.Latin1.GetString(buffer, 0, read));
                }
                catch (OperationCanceledException)
                {
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Reads a complete response, accumulating bytes until a short quiet period elapses.
        /// </summary>
        /// <param name="overall">Maximum total time to wait for the first byte.</param>
        /// <returns>The accumulated response decoded with Latin1.</returns>
        /// <remarks>
        /// After the first chunk arrives, subsequent chunks are gathered with a brief idle window
        /// so that multi-frame replies (for example RESP arrays) are read in their entirety.
        /// </remarks>
        public async Task<string> ReadResponseAsync(TimeSpan overall)
        {
            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[8192];

            // First read waits up to the overall timeout.
            if (!await ReadChunk(buffer, sb, overall).ConfigureAwait(false))
                return sb.ToString();

            // Subsequent reads use a short idle window to catch stragglers.
            while (await ReadChunk(buffer, sb, TimeSpan.FromMilliseconds(150)).ConfigureAwait(false))
            {
            }

            return sb.ToString();
        }

        /// <summary>
        /// Abruptly closes the connection with no graceful shutdown (simulates a client crash).
        /// </summary>
        public void HardClose()
        {
            try
            {
                if (_TcpClient != null) _TcpClient.Client.Close(0);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Releases all resources used by the client, gracefully closing the connection.
        /// </summary>
        public void Dispose()
        {
            try { _Stream?.Dispose(); } catch { }
            try { _TcpClient?.Close(); } catch { }
            _Stream = null;
            _TcpClient = null;
        }

        #endregion


        #region Private-Methods

        /// <summary>
        /// Reads a single chunk into the accumulator, returning false when the read times out or the socket closes.
        /// </summary>
        /// <param name="buffer">A scratch buffer.</param>
        /// <param name="sb">The accumulator.</param>
        /// <param name="timeout">The per-chunk timeout.</param>
        /// <returns>True if data was read; otherwise, false.</returns>
        private async Task<bool> ReadChunk(byte[] buffer, StringBuilder sb, TimeSpan timeout)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(timeout))
            {
                try
                {
                    int read = await _Stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                    if (read <= 0) return false;
                    sb.Append(Encoding.Latin1.GetString(buffer, 0, read));
                    return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (System.IO.IOException)
                {
                    return false;
                }
            }
        }

        #endregion

    }
}
