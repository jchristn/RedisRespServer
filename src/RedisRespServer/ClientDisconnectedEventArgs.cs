namespace RedisResp
{
    using System;
    using System.Net;

    /// <summary>
    /// Provides data for events raised when a client disconnects from the server.
    /// </summary>
    /// <remarks>
    /// This event argument class contains information about a disconnected client,
    /// including the client identifier, reason for disconnection, remote endpoint, and disconnection timestamp.
    /// It is used by the ClientDisconnected event in the RespListener.
    /// </remarks>
    public class ClientDisconnectedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the unique identifier of the disconnected client.
        /// </summary>
        /// <value>The GUID that identified the client session.</value>
        public Guid GUID { get; set; }

        /// <summary>
        /// Gets or sets the reason for the client disconnection.
        /// </summary>
        /// <value>A descriptive string explaining why the client disconnected.</value>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the remote endpoint information of the disconnected client.
        /// </summary>
        /// <value>The IP address and port of the client connection.</value>
        public IPEndPoint RemoteEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the client disconnected.
        /// </summary>
        /// <value>The UTC timestamp of the disconnection.</value>
        public DateTime DisconnectedAt { get; set; } = DateTime.UtcNow;
    }
}