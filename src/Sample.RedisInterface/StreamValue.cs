namespace Sample.RedisInterface
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RedisResp;

    /// <summary>
    /// Represents a Redis stream value.
    /// </summary>
    public class StreamValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        public override RedisValueType Type => RedisValueType.Stream;

        /// <summary>
        /// Gets the stream entries.
        /// </summary>
        /// <value>The stream data as a list of entries.</value>
        public List<StreamEntry> Entries { get; private set; }

        /// <summary>
        /// Gets or sets the last generated ID.
        /// </summary>
        public long LastId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamValue"/> class.
        /// </summary>
        public StreamValue()
        {
            Entries = new List<StreamEntry>();
            LastId = 0;
        }

        /// <summary>
        /// Adds an entry to the stream.
        /// </summary>
        /// <param name="id">The entry ID, or "*" to auto-generate.</param>
        /// <param name="fields">The field-value pairs.</param>
        /// <returns>The actual ID of the added entry.</returns>
        public string XAdd(string id, params string[] fields)
        {
            if (fields.Length % 2 != 0)
                throw new ArgumentException("Field-value pairs must be provided");

            string actualId;
            if (id == "*")
            {
                // Auto-generate ID based on timestamp and sequence
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                actualId = $"{timestamp}-{++LastId}";
            }
            else
            {
                actualId = id;
                // Update LastId if this is a higher ID
                if (long.TryParse(id.Split('-')[0], out var idNum) && idNum > LastId)
                    LastId = idNum;
            }

            var entry = new StreamEntry { Id = actualId };
            for (int i = 0; i < fields.Length; i += 2)
            {
                entry.Fields[fields[i]] = fields[i + 1];
            }

            Entries.Add(entry);
            return actualId;
        }

        /// <summary>
        /// Reads entries from the stream.
        /// </summary>
        /// <param name="start">The start ID.</param>
        /// <param name="end">The end ID.</param>
        /// <param name="count">The maximum number of entries to return.</param>
        /// <returns>An array of entries.</returns>
        public StreamEntry[] XRange(string start = "-", string end = "+", int count = -1)
        {
            var result = Entries.AsEnumerable();

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
        /// Gets the length of the stream.
        /// </summary>
        /// <returns>The number of entries in the stream.</returns>
        public int XLen() => Entries.Count;

        /// <summary>
        /// Deletes entries from the stream.
        /// </summary>
        /// <param name="ids">The entry IDs to delete.</param>
        /// <returns>The number of entries deleted.</returns>
        public int XDel(params string[] ids)
        {
            int count = 0;
            foreach (var id in ids)
            {
                var index = Entries.FindIndex(e => e.Id == id);
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