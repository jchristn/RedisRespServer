namespace Sample.RedisClient
{
    /// <summary>
    /// Represents the different types of responses in the Redis RESP protocol.
    /// </summary>
    /// <remarks>
    /// This enumeration defines all possible response types that can be received
    /// from a Redis server according to the RESP protocol specification.
    /// </remarks>
    public enum RedisResponseType
    {
        /// <summary>
        /// Simple string response (prefix: +).
        /// </summary>
        SimpleString,

        /// <summary>
        /// Error response (prefix: -).
        /// </summary>
        Error,

        /// <summary>
        /// Integer response (prefix: :).
        /// </summary>
        Integer,

        /// <summary>
        /// Bulk string response (prefix: $).
        /// </summary>
        BulkString,

        /// <summary>
        /// Array response (prefix: *).
        /// </summary>
        Array,

        /// <summary>
        /// Null response (special case for bulk strings or arrays).
        /// </summary>
        Null
    }
}