namespace Sample.RedisInterface
{
    using System;
    using System.Text.Json;
    using RedisResp;

    /// <summary>
    /// Represents a Redis JSON value.
    /// </summary>
    public class JsonValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        public override RedisValueType Type => RedisValueType.Json;

        /// <summary>
        /// Gets or sets the JSON data as a string.
        /// </summary>
        /// <value>The JSON data.</value>
        public string Data { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonValue"/> class.
        /// </summary>
        /// <param name="jsonData">The JSON data.</param>
        public JsonValue(string jsonData = "{}")
        {
            Data = jsonData ?? "{}";
        }

        /// <summary>
        /// Sets JSON data at the specified path.
        /// </summary>
        /// <param name="path">The JSON path.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="conditions">Optional conditions (NX, XX).</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        public bool JsonSet(string path, string value, string? conditions = null)
        {
            try
            {
                if (path == ".")
                {
                    // Root path - replace entire document
                    if (conditions == "NX" && !string.IsNullOrEmpty(Data) && Data != "{}")
                        return false; // Key already exists
                    if (conditions == "XX" && (string.IsNullOrEmpty(Data) || Data == "{}"))
                        return false; // Key doesn't exist
                        
                    Data = value;
                    return true;
                }
                
                // For simplicity, just store as-is for non-root paths
                // A full implementation would need a proper JSON library
                Data = value;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets JSON data at the specified path.
        /// </summary>
        /// <param name="path">The JSON path.</param>
        /// <returns>The JSON data at the path, or null if not found.</returns>
        public string? JsonGet(string path = ".")
        {
            if (path == ".")
                return Data;
            
            // For simplicity, return the entire document for any path
            // A full implementation would need proper JSON path parsing
            return Data;
        }

        /// <summary>
        /// Deletes JSON data at the specified path.
        /// </summary>
        /// <param name="path">The JSON path.</param>
        /// <returns>The number of elements deleted.</returns>
        public int JsonDel(string path)
        {
            if (path == ".")
            {
                Data = "{}";
                return 1;
            }
            
            // For simplicity, clear the entire document
            Data = "{}";
            return 1;
        }
    }
}