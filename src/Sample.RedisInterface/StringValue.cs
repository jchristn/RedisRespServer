namespace Sample.RedisInterface
{
    using System;
    using RedisResp;

    /// <summary>
    /// Represents a Redis string value.
    /// </summary>
    public class StringValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        public override RedisValueType Type => RedisValueType.String;

        /// <summary>
        /// Gets or sets the string data.
        /// </summary>
        /// <value>The string value.</value>
        public string Data { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringValue"/> class.
        /// </summary>
        public StringValue()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringValue"/> class with the specified data.
        /// </summary>
        /// <param name="data">The string data.</param>
        public StringValue(string data)
        {
            Data = data ?? string.Empty;
        }

        /// <summary>
        /// Increments the string value if it represents a number.
        /// </summary>
        /// <returns>The new value after increment.</returns>
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
        /// <returns>The new length of the string.</returns>
        public int Append(string text)
        {
            Data += text ?? "";
            return Data.Length;
        }
    }
}