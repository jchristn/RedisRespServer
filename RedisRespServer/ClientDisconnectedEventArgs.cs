namespace RedisResp
{
    using System;

    /// <summary>
    /// Provides data for events raised when a client disconnects from the server.
    /// </summary>
    /// <remarks>
    /// This event argument class contains information about a disconnected client,
    /// including the client identifier, reason for disconnection, and disconnection timestamp.
    /// It is used by the ClientDisconnected event in the RespListener.
    /// </remarks>
    public class ClientDisconnectedEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Gets or sets the unique identifier of the disconnected client.
        /// </summary>
        /// <value>The GUID string that identified the client session.</value>
        public string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the reason for the client disconnection.
        /// </summary>
        /// <value>A descriptive string explaining why the client disconnected.</value>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the client disconnected.
        /// </summary>
        /// <value>The UTC timestamp of the disconnection.</value>
        public DateTime DisconnectedAt { get; set; } = DateTime.UtcNow;

        #endregion
    }
}