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

        /// <summary>
        /// Gets or sets the unique identifier of the client that sent the data.
        /// </summary>
        /// <value>The GUID of the client that sent the RESP data.</value>
        public Guid ClientGUID { get; set; }

        /// <summary>
        /// Gets or sets the raw binary data for bulk strings.
        /// </summary>
        /// <value>The original binary data before string conversion, used to preserve binary content.</value>
        /// <remarks>This is only populated for BulkString data types to preserve binary data integrity.</remarks>
        public byte[] RawBytes { get; set; }

        /// <summary>
        /// Gets or sets the complete unmolested message bytes as received from the network.
        /// </summary>
        /// <value>The original binary message data including all RESP protocol markers and terminators.</value>
        /// <remarks>Contains the complete raw message bytes for all data types, useful for protocol analysis and debugging.</remarks>
        public byte[] MessageBytes { get; set; }

        /// <summary>
        /// Gets or sets the RESP protocol version used to parse this message.
        /// </summary>
        /// <value>The RESP protocol version (RESP2 or RESP3) that was used to parse this message.</value>
        /// <remarks>
        /// This indicates which protocol parsing logic was used for this message.
        /// RESP2 supports basic data types, while RESP3 includes enhanced data types.
        /// </remarks>
        public RespVersionEnum ProtocolVersion { get; set; } = RespVersionEnum.RESP2;
    }
}