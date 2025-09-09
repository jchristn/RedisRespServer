namespace Sample.RedisServer
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using RedisResp;

    /// <summary>
    /// Represents a Redis hash value.
    /// </summary>
    public class HashValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        public override RedisValueType Type => RedisValueType.Hash;

        /// <summary>
        /// Gets the hash field-value pairs.
        /// </summary>
        /// <value>The hash data as a dictionary.</value>
        public ConcurrentDictionary<string, string> Fields { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HashValue"/> class.
        /// </summary>
        public HashValue()
        {
            Fields = new ConcurrentDictionary<string, string>();
        }

        /// <summary>
        /// Sets a field in the hash.
        /// </summary>
        /// <param name="field">The field name.</param>
        /// <param name="value">The field value.</param>
        /// <returns>True if the field was newly created; false if it was updated.</returns>
        public bool SetField(string field, string value)
        {
            return Fields.TryAdd(field, value) || (Fields[field] = value) != null;
        }

        /// <summary>
        /// Gets a field from the hash.
        /// </summary>
        /// <param name="field">The field name.</param>
        /// <returns>The field value, or null if the field doesn't exist.</returns>
        public string? GetField(string field)
        {
            Fields.TryGetValue(field, out string? value);
            return value;
        }

        /// <summary>
        /// Removes a field from the hash.
        /// </summary>
        /// <param name="field">The field name.</param>
        /// <returns>True if the field was removed; false if it didn't exist.</returns>
        public bool RemoveField(string field)
        {
            return Fields.TryRemove(field, out _);
        }

        /// <summary>
        /// Gets all field-value pairs from the hash.
        /// </summary>
        /// <returns>An array of alternating field names and values.</returns>
        public string[] GetAll()
        {
            var result = new List<string>();
            foreach (var kvp in Fields)
            {
                result.Add(kvp.Key);
                result.Add(kvp.Value);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Gets the number of fields in the hash.
        /// </summary>
        /// <returns>The field count.</returns>
        public int FieldCount => Fields.Count;
    }
}