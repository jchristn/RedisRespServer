namespace Redish.Server.Handlers
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Text;
    using RedisResp;
    using Redish.Server.Models;
    using Redish.Server.Storage;
    using SyslogLogging;

    /// <summary>
    /// Handles Redis Stream data type commands including XADD, XRANGE, XLEN, XDEL, and XINFO.
    /// </summary>
    /// <remarks>
    /// This class provides implementations for Redis Stream operations, managing the storage and retrieval
    /// of stream values with proper error handling and RESP protocol formatting.
    /// </remarks>
    public class StreamHandler
    {
        private readonly StorageBase _Storage;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[StreamHandler]";

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamHandler"/> class.
        /// </summary>
        /// <param name="logging">The logging instance for operation tracking.</param>
        /// <param name="storage">The storage instance for Redis values.</param>
        /// <exception cref="ArgumentNullException">Thrown when storage or logging is null.</exception>
        public StreamHandler(LoggingModule logging, StorageBase storage)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// Handles the XADD command to add entries to a stream.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key, entry ID, and field-value pairs.</param>
        /// <returns>A RESP-formatted response with the entry ID that was added.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleXaddCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 4 || (commandArgs.Length - 3) % 2 != 0)
                return "-ERR wrong number of arguments for 'xadd' command\r\n";

            string key = commandArgs[1];
            string id = commandArgs[2];
            string[] fields = commandArgs.Skip(3).ToArray();
            
            StreamValue streamValue;
            if (_Storage.TryGetValue(key, out RedisValue existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is StreamValue existing)
                    streamValue = existing;
                else
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
            }
            else
            {
                streamValue = new StreamValue();
                _Storage.AddOrUpdate(key, streamValue, (k, v) => streamValue);
            }

            try
            {
                string actualId = streamValue.XAdd(id, fields);
                _Logging.Debug(_Header + $"Executed: XADD {key} {id} -> {actualId}");
                return $"${actualId.Length}\r\n{actualId}\r\n";
            }
            catch (Exception ex)
            {
                return $"-ERR {ex.Message}\r\n";
            }
        }

        /// <summary>
        /// Handles the XRANGE command to get entries from a stream within a range.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key, start, end, and optional count.</param>
        /// <returns>A RESP-formatted response with the entries in the specified range.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleXrangeCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 4)
                return "-ERR wrong number of arguments for 'xrange' command\r\n";

            string key = commandArgs[1];
            string start = commandArgs[2];
            string end = commandArgs[3];
            int count = commandArgs.Length > 4 && commandArgs[4].ToUpper() == "COUNT" && commandArgs.Length > 5 ? 
                (int.TryParse(commandArgs[5], out int c) ? c : -1) : -1;
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is StreamValue streamValue)
                {
                    StreamEntry[] entries = streamValue.XRange(start, end, count);
                    StringBuilder response = new StringBuilder();
                    response.Append($"*{entries.Length}\r\n");
                    foreach (StreamEntry entry in entries)
                    {
                        response.Append($"*2\r\n"); // Entry array: [id, fields]
                        response.Append($"${entry.Id.Length}\r\n{entry.Id}\r\n");
                        
                        response.Append($"*{entry.Fields.Count * 2}\r\n"); // Fields array
                        foreach (System.Collections.Generic.KeyValuePair<string, string> field in entry.Fields)
                        {
                            response.Append($"${field.Key.Length}\r\n{field.Key}\r\n");
                            response.Append($"${field.Value.Length}\r\n{field.Value}\r\n");
                        }
                    }
                    _Logging.Debug(_Header + $"Executed: XRANGE {key} {start} {end} -> {entries.Length} entries");
                    return response.ToString();
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: XRANGE {key} {start} {end} -> empty stream");
                return "*0\r\n";
            }
        }

        /// <summary>
        /// Handles the XLEN command to get the length of a stream.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key.</param>
        /// <returns>A RESP-formatted response with the number of entries in the stream.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleXlenCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'xlen' command\r\n";

            string key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is StreamValue streamValue)
                {
                    int length = streamValue.XLen();
                    _Logging.Debug(_Header + $"Executed: XLEN {key} -> {length}");
                    return $":{length}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: XLEN {key} -> 0");
                return ":0\r\n";
            }
        }

        /// <summary>
        /// Handles the XDEL command to delete entries from a stream.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key and entry IDs to delete.</param>
        /// <returns>A RESP-formatted response indicating the number of entries deleted.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleXdelCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 3)
                return "-ERR wrong number of arguments for 'xdel' command\r\n";

            string key = commandArgs[1];
            string[] ids = commandArgs.Skip(2).ToArray();
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is StreamValue streamValue)
                {
                    int deleted = streamValue.XDel(ids);
                    _Logging.Debug(_Header + $"Executed: XDEL {key} -> {deleted}");
                    return $":{deleted}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: XDEL {key} -> 0");
                return ":0\r\n";
            }
        }

        /// <summary>
        /// Handles the XINFO command to get information about streams, groups, or consumers.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the subcommand and parameters.</param>
        /// <returns>A RESP-formatted response with the requested information.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleXinfoCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 2)
                return "-ERR wrong number of arguments for 'xinfo' command\r\n";

            string subcommand = commandArgs[1].ToUpper();
            
            switch (subcommand)
            {
                case "STREAM":
                    if (commandArgs.Length < 3)
                        return "-ERR wrong number of arguments for 'xinfo stream' command\r\n";
                    return HandleXinfoStreamCommand(commandArgs);
                    
                case "GROUPS":
                    if (commandArgs.Length < 3)
                        return "-ERR wrong number of arguments for 'xinfo groups' command\r\n";
                    return HandleXinfoGroupsCommand(commandArgs);
                    
                case "CONSUMERS":
                    if (commandArgs.Length < 4)
                        return "-ERR wrong number of arguments for 'xinfo consumers' command\r\n";
                    return HandleXinfoConsumersCommand(commandArgs);
                    
                default:
                    return $"-ERR unknown subcommand or wrong number of arguments for '{subcommand}'.\r\n";
            }
        }

        /// <summary>
        /// Handles the XINFO STREAM subcommand to get stream information.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key.</param>
        /// <returns>A RESP-formatted response with detailed stream information.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleXinfoStreamCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            string key = commandArgs[2];
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is StreamValue streamValue)
                {
                    StringBuilder response = new StringBuilder();
                    response.Append("*14\r\n"); // Stream info has 14 fields
                    
                    // length
                    response.Append("$6\r\nlength\r\n");
                    response.Append($":{streamValue.XLen()}\r\n");
                    
                    // radix-tree-keys
                    response.Append("$16\r\nradix-tree-keys\r\n");
                    response.Append(":1\r\n");
                    
                    // radix-tree-nodes
                    response.Append("$17\r\nradix-tree-nodes\r\n");
                    response.Append(":2\r\n");
                    
                    // groups
                    response.Append("$6\r\ngroups\r\n");
                    response.Append(":0\r\n");
                    
                    // last-generated-id
                    response.Append("$17\r\nlast-generated-id\r\n");
                    response.Append($"${streamValue.LastId.ToString().Length + 2}\r\n{streamValue.LastId}-0\r\n");
                    
                    // entries-added
                    response.Append("$13\r\nentries-added\r\n");
                    response.Append($":{streamValue.XLen()}\r\n");
                    
                    // recorded-first-entry-id
                    response.Append("$23\r\nrecorded-first-entry-id\r\n");
                    if (streamValue.Entries.Count > 0)
                        response.Append($"${streamValue.Entries[0].Id.Length}\r\n{streamValue.Entries[0].Id}\r\n");
                    else
                        response.Append("$-1\r\n");
                    
                    _Logging.Debug(_Header + $"Executed: XINFO STREAM {key} -> stream info");
                    return response.ToString();
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: XINFO STREAM {key} -> no such key");
                return "-ERR no such key\r\n";
            }
        }

        /// <summary>
        /// Handles the XINFO GROUPS subcommand to get consumer group information.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key.</param>
        /// <returns>A RESP-formatted response with consumer group information.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleXinfoGroupsCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            string key = commandArgs[2];
            
            // For simplicity, return empty array since we don't implement consumer groups
            _Logging.Debug(_Header + $"Executed: XINFO GROUPS {key} -> empty");
            return "*0\r\n";
        }

        /// <summary>
        /// Handles the XINFO CONSUMERS subcommand to get consumer information.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key and group name.</param>
        /// <returns>A RESP-formatted response with consumer information.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleXinfoConsumersCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            string key = commandArgs[2];
            string group = commandArgs[3];
            
            // For simplicity, return empty array since we don't implement consumer groups
            _Logging.Debug(_Header + $"Executed: XINFO CONSUMERS {key} {group} -> empty");
            return "*0\r\n";
        }
    }
}