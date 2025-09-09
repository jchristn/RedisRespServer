namespace Redish.Server.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RedisResp;

    /// <summary>
    /// Represents a single entry in a Redis stream.
    /// </summary>
    /// <remarks>
    /// A stream entry consists of a unique ID and a collection of field-value pairs.
    /// The ID is typically in the format "timestamp-sequence" and entries are ordered
    /// by their IDs within the stream.
    /// </remarks>
    public class StreamEntry
    {
        /// <summary>
        /// Gets or sets the unique identifier for this stream entry.
        /// </summary>
        /// <value>
        /// A string representing the entry ID, typically in the format "timestamp-sequence"
        /// where timestamp is a Unix millisecond timestamp and sequence is an incremental number.
        /// </value>
        public string Id { get; set; } = "";

        /// <summary>
        /// Gets or sets the field-value pairs contained in this stream entry.
        /// </summary>
        /// <value>
        /// A dictionary containing the data fields for this entry, where keys are field names
        /// and values are the corresponding field values, both as strings.
        /// </value>
        public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Represents a Redis stream value that stores an ordered sequence of entries.
    /// </summary>
    /// <remarks>
    /// A Redis stream is an append-only log-like data structure that stores entries
    /// with unique IDs and field-value pairs. Streams are commonly used for event
    /// logging, message queues, and time-series data. Each entry has an automatically
    /// generated or manually specified ID that determines its position in the stream.
    /// Streams support operations like adding entries, reading ranges, and deleting entries.
    /// </remarks>
    public class StreamValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        /// <value>Always returns <see cref="RedisValueType.Stream"/>.</value>
        public override RedisValueType Type => RedisValueType.Stream;

        /// <summary>
        /// Gets the ordered collection of stream entries.
        /// </summary>
        /// <value>
        /// A list containing all stream entries in the order they were added.
        /// Entries are ordered by their IDs, with newer entries typically having higher IDs.
        /// </value>
        public List<StreamEntry> Entries { get; private set; }

        /// <summary>
        /// Gets or sets the last generated ID sequence number.
        /// </summary>
        /// <value>
        /// The sequence number used for auto-generating entry IDs. This value is
        /// incremented each time a new entry is added with an auto-generated ID.
        /// </value>
        public long LastId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamValue"/> class.
        /// </summary>
        /// <remarks>
        /// Creates an empty stream with no entries and initializes the last ID sequence
        /// number to zero. Use the various methods to add, read, or delete entries.
        /// </remarks>
        public StreamValue()
        {
            Entries = new List<StreamEntry>();
            LastId = 0;
        }

        /// <summary>
        /// Adds a new entry to the stream with the specified ID and field-value pairs.
        /// </summary>
        /// <param name="id">
        /// The entry ID to use, or "*" to auto-generate an ID based on the current timestamp.
        /// </param>
        /// <param name="fields">
        /// An array containing alternating field names and values. Must contain an even
        /// number of elements where even indices are field names and odd indices are values.
        /// </param>
        /// <returns>
        /// The actual ID of the entry that was added to the stream. For auto-generated IDs,
        /// this will be in the format "timestamp-sequence".
        /// </returns>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="fields"/> does not contain an even number of elements.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="id"/> or <paramref name="fields"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis XADD command. If the ID is "*",
        /// a unique ID is generated using the current Unix timestamp in milliseconds
        /// and an incremental sequence number. The entry is appended to the end of the stream.
        /// </remarks>
        public string XAdd(string id, params string[] fields)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            if (fields == null)
                throw new ArgumentNullException(nameof(fields));

            if (fields.Length % 2 != 0)
                throw new ArgumentException("Field-value pairs must be provided", nameof(fields));

            string actualId;
            if (id == "*")
            {
                // Auto-generate ID based on timestamp and sequence
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                actualId = $"{timestamp}-{++LastId}";
            }
            else
            {
                actualId = id;
                // Update LastId if this is a higher ID
                if (long.TryParse(id.Split('-')[0], out long idNum) && idNum > LastId)
                    LastId = idNum;
            }

            StreamEntry entry = new StreamEntry { Id = actualId };
            for (int i = 0; i < fields.Length; i += 2)
            {
                entry.Fields[fields[i]] = fields[i + 1];
            }

            Entries.Add(entry);
            return actualId;
        }

        /// <summary>
        /// Reads a range of entries from the stream within the specified ID bounds.
        /// </summary>
        /// <param name="start">
        /// The starting ID for the range, or "-" to start from the beginning of the stream.
        /// </param>
        /// <param name="end">
        /// The ending ID for the range, or "+" to read until the end of the stream.
        /// </param>
        /// <param name="count">
        /// The maximum number of entries to return, or -1 for no limit.
        /// </param>
        /// <returns>
        /// An array of stream entries within the specified range, ordered by their IDs.
        /// Returns an empty array if no entries match the criteria.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="start"/> or <paramref name="end"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis XRANGE command. The range is inclusive
        /// on both ends. Special values "-" and "+" can be used to represent the smallest
        /// and largest possible IDs, respectively. Entry IDs are compared lexicographically.
        /// </remarks>
        public StreamEntry[] XRange(string start = "-", string end = "+", int count = -1)
        {
            if (start == null)
                throw new ArgumentNullException(nameof(start));
            if (end == null)
                throw new ArgumentNullException(nameof(end));

            IEnumerable<StreamEntry> result = Entries.AsEnumerable();

            // Filter by start
            if (start != "-")
            {
                result = result.Where(e => string.Compare(e.Id, start, StringComparison.Ordinal) >= 0);
            }

            // Filter by end
            if (end != "+")
            {
                result = result.Where(e => string.Compare(e.Id, end, StringComparison.Ordinal) <= 0);
            }

            // Limit count
            if (count > 0)
            {
                result = result.Take(count);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Gets the total number of entries in the stream.
        /// </summary>
        /// <returns>The count of entries currently stored in the stream.</returns>
        /// <remarks>
        /// This operation is equivalent to the Redis XLEN command and provides
        /// the total number of entries that have been added to the stream and
        /// have not been deleted.
        /// </remarks>
        public int XLen() => Entries.Count;

        /// <summary>
        /// Deletes one or more entries from the stream by their IDs.
        /// </summary>
        /// <param name="ids">The entry IDs to delete from the stream.</param>
        /// <returns>
        /// The number of entries that were actually deleted. Entries with IDs
        /// that don't exist in the stream are not counted.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="ids"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis XDEL command. Only entries
        /// with matching IDs are removed from the stream. The order of remaining
        /// entries is preserved after deletion.
        /// </remarks>
        public int XDel(params string[] ids)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            int count = 0;
            foreach (string id in ids)
            {
                int index = Entries.FindIndex(e => e.Id == id);
                if (index >= 0)
                {
                    Entries.RemoveAt(index);
                    count++;
                }
            }
            return count;
        }
    }
}