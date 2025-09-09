namespace Sample.RedisInterface
{
    using System;
    using RedisResp;

    /// <summary>
    /// Represents information about a connected Redis client.
    /// </summary>
    /// <remarks>
    /// This class stores client-specific information used by administrative
    /// commands such as CLIENT SETNAME, CLIENT SETINFO, and CLIENT ID.
    /// </remarks>
    public class ClientInfo
    {
        /// <summary>
        /// Gets or sets the unique client identifier assigned by the server.
        /// </summary>
        /// <value>The client ID.</value>
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
        /// <value>The connection timestamp.</value>
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the RESP protocol version negotiated with this client.
        /// </summary>
        /// <value>The RESP version, defaults to RESP2.</value>
        public RespVersionEnum RespVersion { get; set; } = RespVersionEnum.RESP2;
    }
}