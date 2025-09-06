namespace RedisResp
{
    /// <summary>
    /// Represents the different data types supported by the Redis Serialization Protocol (RESP).
    /// </summary>
    /// <remarks>
    /// RESP is a serialization protocol used by Redis for client-server communication.
    /// Each data type is identified by a specific first character in the protocol.
    /// </remarks>
    public enum RespDataType
    {
        /// <summary>
        /// Simple string data type (prefix: +).
        /// Used for simple status replies like "OK".
        /// </summary>
        SimpleString,

        /// <summary>
        /// Error data type (prefix: -).
        /// Used to represent error messages from the server.
        /// </summary>
        Error,

        /// <summary>
        /// Integer data type (prefix: :).
        /// Used for signed 64-bit integers.
        /// </summary>
        Integer,

        /// <summary>
        /// Bulk string data type (prefix: $).
        /// Used for binary-safe strings with explicit length.
        /// </summary>
        BulkString,

        /// <summary>
        /// Array data type (prefix: *).
        /// Used for ordered collections of RESP elements.
        /// </summary>
        Array,

        /// <summary>
        /// Null data type.
        /// Represents null values in bulk strings or arrays.
        /// </summary>
        Null
    }
}