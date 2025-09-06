namespace RedisResp
{
    using System;
    using System.Net;

    /// <summary>
    /// Provides data for events raised when a client connects to the server.
    /// </summary>
    /// <remarks>
    /// This event argument class contains information about a newly connected client,
    /// including the unique client identifier, remote endpoint, and connection timestamp.
    /// It is used by the ClientConnected event in the RespListener.
    /// </remarks>
    public class ClientConnectedEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Gets or sets the unique identifier for the connected client.
        /// </summary>
        /// <value>A GUID string uniquely identifying the client session.</value>
        public string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the remote endpoint information of the connected client.
        /// </summary>
        /// <value>The IP address and port of the client connection.</value>
        public IPEndPoint RemoteEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the client connected.
        /// </summary>
        /// <value>The UTC timestamp of the connection establishment.</value>
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

        #endregion
    }
}