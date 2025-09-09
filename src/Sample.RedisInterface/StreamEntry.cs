namespace Sample.RedisInterface
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a Redis stream entry.
    /// </summary>
    public class StreamEntry
    {
        /// <summary>
        /// Gets or sets the entry ID.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Gets or sets the field-value pairs.
        /// </summary>
        public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
    }
}