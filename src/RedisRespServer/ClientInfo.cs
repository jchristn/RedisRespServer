namespace RedisResp
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Contains information about a connected client.
    /// </summary>
    /// <remarks>
    /// This class stores all relevant information about a client connection,
    /// including the unique identifier, TCP client connection, and remote endpoint.
    /// It is used internally by RespListener for client management.
    /// </remarks>
    public class ClientInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the client.
        /// </summary>
        /// <value>A GUID uniquely identifying the client session.</value>
        public Guid GUID { get; set; }

        /// <summary>
        /// Gets or sets the TCP client connection.
        /// </summary>
        /// <value>The underlying TcpClient connection.</value>
        public TcpClient TcpClient { get; set; }

        /// <summary>
        /// Gets or sets the remote endpoint information of the client.
        /// </summary>
        /// <value>The IP address and port of the client connection.</value>
        public IPEndPoint RemoteEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the client connected.
        /// </summary>
        /// <value>The UTC timestamp of the connection establishment.</value>
        public DateTime ConnectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the client name as set by the CLIENT SETNAME command.
        /// </summary>
        /// <value>The name assigned to the client, or null if no name has been set.</value>
        public string Name { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientInfo"/> class.
        /// </summary>
        /// <param name="guid">The unique identifier for the client.</param>
        /// <param name="tcpClient">The TCP client connection.</param>
        /// <param name="remoteEndPoint">The remote endpoint of the client.</param>
        /// <exception cref="ArgumentNullException">Thrown when tcpClient or remoteEndPoint is null.</exception>
        /// <remarks>
        /// Creates a new client info instance with the specified connection details.
        /// The connected timestamp is automatically set to the current UTC time.
        /// </remarks>
        public ClientInfo(Guid guid, TcpClient tcpClient, IPEndPoint remoteEndPoint)
        {
            GUID = guid;
            TcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            ConnectedUtc = DateTime.UtcNow;
        }
    }
}