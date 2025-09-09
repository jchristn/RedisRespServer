namespace RedisResp
{
    using System;

    /// <summary>
    /// Provides data for events raised when an error occurs in the server.
    /// </summary>
    /// <remarks>
    /// This event argument class contains information about server or client errors,
    /// including the error message, underlying exception, and associated client ID if applicable.
    /// It is used by the ErrorOccurred event in the RespListener.
    /// </remarks>
    public class ErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the error message describing what went wrong.
        /// </summary>
        /// <value>A human-readable description of the error.</value>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the exception that caused the error, if any.
        /// </summary>
        /// <value>The underlying exception, or null if no exception was thrown.</value>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the client associated with the error.
        /// </summary>
        /// <value>The client GUID if the error is client-specific, or null for server-wide errors.</value>
        public Guid? GUID { get; set; }
    }
}