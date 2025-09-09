namespace RedisResp
{
    /// <summary>
    /// Enumeration of Redis value types.
    /// </summary>
    public enum RedisValueType
    {
        /// <summary>
        /// String value type.
        /// </summary>
        String,
        
        /// <summary>
        /// Hash value type.
        /// </summary>
        Hash,
        
        /// <summary>
        /// List value type.
        /// </summary>
        List,
        
        /// <summary>
        /// Set value type.
        /// </summary>
        Set,
        
        /// <summary>
        /// Sorted set value type.
        /// </summary>
        SortedSet,
        
        /// <summary>
        /// JSON value type.
        /// </summary>
        Json,
        
        /// <summary>
        /// Stream value type.
        /// </summary>
        Stream
    }
}