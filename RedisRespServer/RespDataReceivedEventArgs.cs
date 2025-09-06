namespace RedisResp
{
    using System;

    /// <summary>
    /// Provides data for events raised when RESP data is received.
    /// </summary>
    /// <remarks>
    /// This event argument class contains all the information about a received RESP protocol message,
    /// including the parsed data type, the actual value, raw protocol data, and timestamp.
    /// It is used by all RESP data type events in the RespListener.
    /// </remarks>
    public class RespDataReceivedEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Gets or sets the type of RESP data that was received.
        /// </summary>
        /// <value>The RESP data type enumeration value.</value>
        public RespDataType DataType { get; set; }

        /// <summary>
        /// Gets or sets the parsed value of the received data.
        /// </summary>
        /// <value>
        /// The parsed data value. Type varies based on DataType:
        /// - SimpleString/Error: string
        /// - Integer: long or string (if parsing failed)
        /// - BulkString: string
        /// - Array: object with Length and Data properties
        /// - Null: null
        /// </value>
        public object Value { get; set; }

        /// <summary>
        /// Gets or sets the raw RESP protocol data as received.
        /// </summary>
        /// <value>The unparsed raw data string including RESP protocol markers.</value>
        public string RawData { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the data was received.
        /// </summary>
        /// <value>The UTC timestamp of when the data was processed.</value>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        #endregion
    }
}