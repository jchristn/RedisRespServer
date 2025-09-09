namespace RedisResp
{
    /// <summary>
    /// Represents the result of parsing a RESP message.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the parsed message data along with metadata about the parsing operation,
    /// including the original raw data, the number of bytes consumed during parsing, and optionally
    /// the raw bytes from bulk string content for binary data preservation.
    /// </remarks>
    internal class RespParseResult
    {
        /// <summary>
        /// Gets or sets the parsed message object.
        /// </summary>
        /// <value>The message object extracted from the RESP data, which can be a string, integer, array, or null.</value>
        public object Message { get; set; }

        /// <summary>
        /// Gets or sets the raw RESP data string.
        /// </summary>
        /// <value>The original RESP protocol string that was parsed.</value>
        public string RawData { get; set; }

        /// <summary>
        /// Gets or sets the raw bytes from bulk string content.
        /// </summary>
        /// <value>The binary data extracted from bulk strings, or null if the message doesn't contain bulk string data.</value>
        public byte[] RawBytes { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes consumed during parsing.
        /// </summary>
        /// <value>The count of bytes that were processed from the input buffer.</value>
        public int BytesConsumed { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RespParseResult"/> class.
        /// </summary>
        /// <param name="message">The parsed message object.</param>
        /// <param name="rawData">The raw RESP data string.</param>
        /// <param name="bytesConsumed">The number of bytes consumed during parsing.</param>
        /// <param name="rawBytes">The raw bytes from bulk string content.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when rawData is null.</exception>
        public RespParseResult(object message, string rawData, int bytesConsumed, byte[] rawBytes = null)
        {
            Message = message;
            RawData = rawData ?? throw new System.ArgumentNullException(nameof(rawData));
            RawBytes = rawBytes;
            BytesConsumed = bytesConsumed;
        }
    }
}