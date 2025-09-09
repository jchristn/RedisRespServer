namespace Redish.Server.Handlers
{
    using System;
    using System.Collections.Concurrent;
    using RedisResp;
    using Redish.Server.Models;
    using Redish.Server.Storage;
    using SyslogLogging;

    /// <summary>
    /// Handles Redis JSON data type commands including JSON.SET, JSON.GET, and JSON.DEL.
    /// </summary>
    /// <remarks>
    /// This class provides implementations for Redis JSON operations, managing the storage and retrieval
    /// of JSON values with proper error handling and RESP protocol formatting.
    /// </remarks>
    public class JsonHandler
    {
        private readonly StorageBase _Storage;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[JsonHandler]";

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonHandler"/> class.
        /// </summary>
        /// <param name="logging">The logging instance for operation tracking.</param>
        /// <param name="storage">The storage instance for Redis values.</param>
        /// <exception cref="ArgumentNullException">Thrown when storage or logging is null.</exception>
        public JsonHandler(LoggingModule logging, StorageBase storage)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// Handles the JSON.SET command to set JSON values at specified paths.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key, path, JSON value, and optional condition.</param>
        /// <returns>A RESP-formatted response indicating success or failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleJsonSetCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 4)
                return "-ERR wrong number of arguments for 'json.set' command\r\n";

            string key = commandArgs[1];
            string path = commandArgs[2];
            string jsonValue = commandArgs[3];
            string? condition = commandArgs.Length > 4 ? commandArgs[4] : null;
            
            JsonValue jsonValueObj;
            if (_Storage.TryGetValue(key, out RedisValue existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is JsonValue existing)
                    jsonValueObj = existing;
                else
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
            }
            else
            {
                jsonValueObj = new JsonValue();
                _Storage.AddOrUpdate(key, jsonValueObj, (k, v) => jsonValueObj);
            }

            bool success = jsonValueObj.JsonSet(path, jsonValue, condition);
            if (success)
            {
                _Logging.Debug(_Header + $"Executed: JSON.SET {key} {path} -> OK");
                return "+OK\r\n";
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: JSON.SET {key} {path} -> (nil)");
                return "$-1\r\n";
            }
        }

        /// <summary>
        /// Handles the JSON.GET command to get JSON values from specified paths.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key and optional path.</param>
        /// <returns>A RESP-formatted response with the JSON value or nil if not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleJsonGetCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 2)
                return "-ERR wrong number of arguments for 'json.get' command\r\n";

            string key = commandArgs[1];
            string path = commandArgs.Length > 2 ? commandArgs[2] : ".";
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is JsonValue jsonValue)
                {
                    string? result = jsonValue.JsonGet(path);
                    if (result != null)
                    {
                        _Logging.Debug(_Header + $"Executed: JSON.GET {key} {path} -> {result.Length} bytes");
                        return $"${result.Length}\r\n{result}\r\n";
                    }
                    else
                    {
                        _Logging.Debug(_Header + $"Executed: JSON.GET {key} {path} -> (nil)");
                        return "$-1\r\n";
                    }
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: JSON.GET {key} {path} -> (nil)");
                return "$-1\r\n";
            }
        }

        /// <summary>
        /// Handles the JSON.DEL command to delete JSON values at specified paths.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key and optional path.</param>
        /// <returns>A RESP-formatted response indicating the number of elements deleted.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleJsonDelCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 2)
                return "-ERR wrong number of arguments for 'json.del' command\r\n";

            string key = commandArgs[1];
            string path = commandArgs.Length > 2 ? commandArgs[2] : ".";
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is JsonValue jsonValue)
                {
                    int deleted = jsonValue.JsonDel(path);
                    _Logging.Debug(_Header + $"Executed: JSON.DEL {key} {path} -> {deleted}");
                    return $":{deleted}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: JSON.DEL {key} {path} -> 0");
                return ":0\r\n";
            }
        }
    }
}