namespace RedisResp
{
    /// <summary>
    /// Represents the version of the Redis Serialization Protocol (RESP) used for a message.
    /// </summary>
    /// <remarks>
    /// RESP2 is the original protocol version, while RESP3 introduces additional data types
    /// and enhanced capabilities for modern Redis communication.
    /// </remarks>
    public enum RespVersionEnum
    {
        /// <summary>
        /// RESP2 protocol version (original).
        /// Supports: SimpleString, Error, Integer, BulkString, Array, Null.
        /// </summary>
        RESP2 = 2,

        /// <summary>
        /// RESP3 protocol version (enhanced).
        /// Supports all RESP2 types plus: Double, Boolean, BigNumber, BlobError,
        /// VerbatimString, Map, Set, Attribute, Push.
        /// </summary>
        RESP3 = 3
    }
}