namespace Redish.Server.Models
{
    using System;
    using RedisResp;

    /// <summary>
    /// Represents a Redis string value.
    /// </summary>
    /// <remarks>
    /// String values are the most basic data type in Redis and can store
    /// text, numbers, or binary data. They support operations like increment,
    /// decrement, and append for numeric and text manipulation.
    /// </remarks>
    public class StringValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        /// <value>Always returns RedisValueType.String.</value>
        public override RedisValueType Type => RedisValueType.String;

        /// <summary>
        /// Gets or sets the string data.
        /// </summary>
        /// <value>The string value, never null (empty string if not set).</value>
        public string Data { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringValue"/> class.
        /// </summary>
        /// <remarks>
        /// Creates an empty string value. The Data property will be set to an empty string.
        /// </remarks>
        public StringValue()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringValue"/> class with the specified data.
        /// </summary>
        /// <param name="data">The string data to store.</param>
        /// <remarks>
        /// If data is null, it will be converted to an empty string.
        /// </remarks>
        public StringValue(string data)
        {
            Data = data ?? string.Empty;
        }

        /// <summary>
        /// Increments the string value if it represents a number.
        /// </summary>
        /// <returns>The new value after increment.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the value is not a valid integer or is out of range.</exception>
        /// <remarks>
        /// The string must represent a valid 64-bit signed integer.
        /// After incrementing, the Data property is updated with the new value.
        /// </remarks>
        public long Increment()
        {
            if (long.TryParse(Data, out long num))
            {
                num++;
                Data = num.ToString();
                return num;
            }
            throw new InvalidOperationException("value is not an integer or out of range");
        }

        /// <summary>
        /// Decrements the string value if it represents a number.
        /// </summary>
        /// <returns>The new value after decrement.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the value is not a valid integer or is out of range.</exception>
        /// <remarks>
        /// The string must represent a valid 64-bit signed integer.
        /// After decrementing, the Data property is updated with the new value.
        /// </remarks>
        public long Decrement()
        {
            if (long.TryParse(Data, out long num))
            {
                num--;
                Data = num.ToString();
                return num;
            }
            throw new InvalidOperationException("value is not an integer or out of range");
        }

        /// <summary>
        /// Increments the string value by the specified amount if it represents a number.
        /// </summary>
        /// <param name="increment">The amount to increment by.</param>
        /// <returns>The new value after increment.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the value is not a valid integer or is out of range.</exception>
        /// <remarks>
        /// The string must represent a valid 64-bit signed integer.
        /// After incrementing, the Data property is updated with the new value.
        /// </remarks>
        public long IncrementBy(long increment)
        {
            if (long.TryParse(Data, out long num))
            {
                num += increment;
                Data = num.ToString();
                return num;
            }
            throw new InvalidOperationException("value is not an integer or out of range");
        }

        /// <summary>
        /// Appends text to the string value.
        /// </summary>
        /// <param name="text">The text to append.</param>
        /// <returns>The new length of the string after appending.</returns>
        /// <remarks>
        /// If text is null, nothing is appended. The operation modifies
        /// the Data property by concatenating the new text.
        /// </remarks>
        public int Append(string text)
        {
            Data += text ?? "";
            return Data.Length;
        }

        /// <summary>
        /// Returns a string representation of this string value.
        /// </summary>
        /// <returns>The string data stored in this value.</returns>
        public override string ToString()
        {
            return Data;
        }
    }
}