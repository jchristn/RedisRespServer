namespace Redish.Server.Models
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using RedisResp;

    /// <summary>
    /// Represents a Redis hash value that stores field-value pairs.
    /// </summary>
    /// <remarks>
    /// A hash value is a collection of field-value pairs, similar to a dictionary or map.
    /// This implementation uses a thread-safe ConcurrentDictionary to support concurrent operations.
    /// Hash values are commonly used in Redis for storing object-like data structures.
    /// </remarks>
    public class HashValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        /// <value>Always returns <see cref="RedisValueType.Hash"/>.</value>
        public override RedisValueType Type => RedisValueType.Hash;

        /// <summary>
        /// Gets the number of fields in the hash.
        /// </summary>
        /// <value>The total count of field-value pairs in the hash.</value>
        /// <remarks>
        /// This property is equivalent to the Redis HLEN command, which returns
        /// the number of fields contained in a hash.
        /// </remarks>
        public int FieldCount => Fields.Count;

        /// <summary>
        /// Gets the hash field-value pairs.
        /// </summary>
        /// <value>
        /// A thread-safe dictionary containing the hash data as field-value pairs.
        /// The key represents the field name and the value represents the field value.
        /// </value>
        public ConcurrentDictionary<string, string> Fields { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HashValue"/> class.
        /// </summary>
        /// <remarks>
        /// Creates an empty hash with no field-value pairs. Use the various methods
        /// to add, update, or retrieve fields from the hash.
        /// </remarks>
        public HashValue()
        {
            Fields = new ConcurrentDictionary<string, string>();
        }

        /// <summary>
        /// Sets a field in the hash to the specified value.
        /// </summary>
        /// <param name="field">The field name to set.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <returns>
        /// True if the field was newly created; false if an existing field was updated.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="field"/> is null.
        /// </exception>
        /// <remarks>
        /// If the field already exists, its value will be updated. If the field doesn't exist,
        /// it will be created with the specified value.
        /// </remarks>
        public bool SetField(string field, string value)
        {
            return Fields.TryAdd(field, value) || (Fields[field] = value) != null;
        }

        /// <summary>
        /// Gets the value of a field from the hash.
        /// </summary>
        /// <param name="field">The field name to retrieve.</param>
        /// <returns>
        /// The field value if the field exists; otherwise, null.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="field"/> is null.
        /// </exception>
        public string? GetField(string field)
        {
            Fields.TryGetValue(field, out string? value);
            return value;
        }

        /// <summary>
        /// Removes a field from the hash.
        /// </summary>
        /// <param name="field">The field name to remove.</param>
        /// <returns>
        /// True if the field was successfully removed; false if the field didn't exist.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="field"/> is null.
        /// </exception>
        public bool RemoveField(string field)
        {
            return Fields.TryRemove(field, out _);
        }

        /// <summary>
        /// Gets all field-value pairs from the hash as a flat array.
        /// </summary>
        /// <returns>
        /// An array containing alternating field names and values. For example,
        /// if the hash contains {"field1": "value1", "field2": "value2"}, the result
        /// would be ["field1", "value1", "field2", "value2"].
        /// </returns>
        /// <remarks>
        /// This method is commonly used to implement the Redis HGETALL command.
        /// The returned array has an even number of elements, with field names at
        /// even indices and their corresponding values at odd indices.
        /// </remarks>
        public string[] GetAll()
        {
            List<string> result = new List<string>();
            foreach (KeyValuePair<string, string> kvp in Fields)
            {
                result.Add(kvp.Key);
                result.Add(kvp.Value);
            }
            return result.ToArray();
        }
    }
}