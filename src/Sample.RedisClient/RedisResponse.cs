namespace Sample.RedisClient
{
    /// <summary>
    /// Represents a parsed Redis server response.
    /// </summary>
    /// <remarks>
    /// This class encapsulates a Redis server response with its type and value,
    /// providing a structured way to handle different RESP protocol response types.
    /// </remarks>
    public class RedisResponse
    {
        /// <summary>
        /// Gets the type of the Redis response.
        /// </summary>
        /// <value>The response type according to the RESP protocol.</value>
        public RedisResponseType Type { get; }

        /// <summary>
        /// Gets the value of the Redis response.
        /// </summary>
        /// <value>
        /// The response value. Type varies based on response type:
        /// - SimpleString/BulkString/Error: string
        /// - Integer: long
        /// - Array: List&lt;object&gt;
        /// - Null: null
        /// </value>
        public object? Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisResponse"/> class.
        /// </summary>
        /// <param name="type">The type of the response.</param>
        /// <param name="value">The value of the response.</param>
        /// <remarks>
        /// Creates a new Redis response with the specified type and value.
        /// The value should match the expected type for the given response type.
        /// </remarks>
        public RedisResponse(RedisResponseType type, object? value)
        {
            Type = type;
            Value = value;
        }

        /// <summary>
        /// Returns a string representation of the Redis response.
        /// </summary>
        /// <returns>A string describing the response type and value.</returns>
        public override string ToString()
        {
            return $"{Type}: {Value ?? "(null)"}";
        }
    }
}