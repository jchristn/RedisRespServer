namespace Redish.Server.Models
{
    using System;
    using RedisResp;

    /// <summary>
    /// Represents information about a connected Redis client.
    /// </summary>
    /// <remarks>
    /// This class stores client-specific information used by administrative
    /// commands such as CLIENT SETNAME, CLIENT SETINFO, and CLIENT ID.
    /// It tracks client metadata, connection details, and protocol version.
    /// </remarks>
    public class ClientInfo
    {
        /// <summary>
        /// Gets or sets the unique client identifier assigned by the server.
        /// </summary>
        /// <value>The client ID, automatically assigned when the client connects.</value>
        public long ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client name set by CLIENT SETNAME command.
        /// </summary>
        /// <value>The client name or null if not set.</value>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the client library name set by CLIENT SETINFO command.
        /// </summary>
        /// <value>The library name or null if not set.</value>
        public string? LibraryName { get; set; }

        /// <summary>
        /// Gets or sets the client library version set by CLIENT SETINFO command.
        /// </summary>
        /// <value>The library version or null if not set.</value>
        public string? LibraryVersion { get; set; }

        /// <summary>
        /// Gets or sets the time when the client connected.
        /// </summary>
        /// <value>The connection timestamp in UTC.</value>
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the RESP protocol version negotiated with this client.
        /// </summary>
        /// <value>The RESP version, defaults to RESP2.</value>
        public RespVersionEnum RespVersion { get; set; } = RespVersionEnum.RESP2;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientInfo"/> class.
        /// </summary>
        /// <remarks>
        /// Creates a new ClientInfo instance with default values.
        /// The connection time is set to the current UTC time.
        /// </remarks>
        public ClientInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientInfo"/> class with the specified client ID.
        /// </summary>
        /// <param name="clientId">The unique client identifier.</param>
        public ClientInfo(long clientId)
        {
            ClientId = clientId;
        }
    }
}