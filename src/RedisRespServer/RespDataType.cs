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
        Null,

        /// <summary>
        /// Double data type (prefix: ,).
        /// Used for floating-point numbers in RESP3.
        /// </summary>
        Double,

        /// <summary>
        /// Boolean data type (prefix: #).
        /// Used for true/false values in RESP3.
        /// </summary>
        Boolean,

        /// <summary>
        /// BigNumber data type (prefix: ().
        /// Used for arbitrary precision numbers in RESP3.
        /// </summary>
        BigNumber,

        /// <summary>
        /// BlobError data type (prefix: !).
        /// Used for binary-safe error messages in RESP3.
        /// </summary>
        BlobError,

        /// <summary>
        /// VerbatimString data type (prefix: =).
        /// Used for strings with encoding information in RESP3.
        /// </summary>
        VerbatimString,

        /// <summary>
        /// Map data type (prefix: %).
        /// Used for key-value pairs in RESP3.
        /// </summary>
        Map,

        /// <summary>
        /// Set data type (prefix: ~).
        /// Used for unordered collections in RESP3.
        /// </summary>
        Set,

        /// <summary>
        /// Attribute data type (prefix: |).
        /// Used for metadata attributes in RESP3.
        /// </summary>
        Attribute,

        /// <summary>
        /// Push data type (prefix: >).
        /// Used for server-initiated push messages in RESP3.
        /// </summary>
        Push
    }
}