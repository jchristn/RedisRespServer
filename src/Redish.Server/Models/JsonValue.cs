namespace Redish.Server.Models
{
    using System;
    using System.Text.Json;
    using RedisResp;

    /// <summary>
    /// Represents a Redis JSON value that stores structured JSON data.
    /// </summary>
    /// <remarks>
    /// A Redis JSON value contains structured data in JSON format and supports operations
    /// for setting, getting, and deleting data at specific JSON paths. This implementation
    /// provides basic JSON functionality similar to the RedisJSON module, allowing storage
    /// and manipulation of complex data structures within Redis keys. The JSON data is
    /// stored as a string and can be accessed or modified using JSONPath-like syntax.
    /// </remarks>
    public class JsonValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        /// <value>Always returns <see cref="RedisValueType.Json"/>.</value>
        public override RedisValueType Type => RedisValueType.Json;

        /// <summary>
        /// Gets or sets the JSON data as a string representation.
        /// </summary>
        /// <value>
        /// The complete JSON document stored as a string. This should always contain
        /// valid JSON data. Defaults to an empty JSON object ("{}") if not specified.
        /// </value>
        public string Data { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonValue"/> class.
        /// </summary>
        /// <param name="jsonData">
        /// The initial JSON data as a string. If null, defaults to an empty JSON object.
        /// </param>
        /// <remarks>
        /// Creates a new JSON value with the specified JSON data. The data should be
        /// valid JSON, but this constructor does not validate the JSON format.
        /// Use JSON validation methods if strict format checking is required.
        /// </remarks>
        public JsonValue(string jsonData = "{}")
        {
            Data = jsonData ?? "{}";
        }

        /// <summary>
        /// Sets JSON data at the specified path within the JSON document.
        /// </summary>
        /// <param name="path">
        /// The JSON path where to set the value. Use "." for the root path to replace
        /// the entire document, or a JSONPath expression for nested elements.
        /// </param>
        /// <param name="value">The JSON value to set at the specified path.</param>
        /// <param name="conditions">
        /// Optional conditions for the set operation:
        /// - "NX": Only set if the path does not already exist
        /// - "XX": Only set if the path already exists
        /// - null or other values: Set unconditionally
        /// </param>
        /// <returns>
        /// True if the operation succeeded and the value was set; false if the operation
        /// failed due to conditions not being met or other errors.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="path"/> or <paramref name="value"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the RedisJSON JSON.SET command. For the root path ("."),
        /// the entire document is replaced. For other paths, this simplified implementation
        /// currently replaces the entire document with the provided value. A full implementation
        /// would require proper JSONPath parsing and manipulation.
        /// </remarks>
        public bool JsonSet(string path, string value, string? conditions = null)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

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
                // A full implementation would need a proper JSON library with JSONPath support
                Data = value;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets JSON data at the specified path within the JSON document.
        /// </summary>
        /// <param name="path">
        /// The JSON path to retrieve. Use "." for the root path to get the entire
        /// document, or a JSONPath expression for nested elements.
        /// </param>
        /// <returns>
        /// The JSON data at the specified path as a string, or null if the path
        /// is not found or an error occurs during retrieval.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="path"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the RedisJSON JSON.GET command. For the root
        /// path ("."), the entire JSON document is returned. This simplified implementation
        /// currently returns the entire document for any path. A full implementation
        /// would require proper JSONPath parsing and data extraction.
        /// </remarks>
        public string? JsonGet(string path = ".")
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path == ".")
                return Data;
            
            // For simplicity, return the entire document for any path
            // A full implementation would need proper JSON path parsing
            return Data;
        }

        /// <summary>
        /// Deletes JSON data at the specified path within the JSON document.
        /// </summary>
        /// <param name="path">
        /// The JSON path to delete. Use "." for the root path to clear the entire
        /// document, or a JSONPath expression for nested elements.
        /// </param>
        /// <returns>
        /// The number of elements that were deleted from the JSON document.
        /// Returns 1 if the operation succeeded, 0 if nothing was deleted.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="path"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the RedisJSON JSON.DEL command. For the root
        /// path ("."), the entire document is reset to an empty JSON object. This simplified
        /// implementation currently clears the entire document for any path. A full
        /// implementation would require proper JSONPath parsing and selective deletion.
        /// </remarks>
        public int JsonDel(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

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