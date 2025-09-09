namespace Sample.RedisInterface
{
    using System;
    using System.Linq;
    using System.Text;

    public partial class RedisInterfaceServer
    {
        // Sorted Set Commands
        private string HandleZaddCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 4 || (commandArgs.Length - 2) % 2 != 0)
                return "-ERR wrong number of arguments for 'zadd' command\r\n";

            var key = commandArgs[1];
            var scoreMembers = commandArgs.Skip(2).Cast<object>().ToArray();
            
            SortedSetValue sortedSetValue;
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is SortedSetValue existing)
                    sortedSetValue = existing;
                else
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
            }
            else
            {
                sortedSetValue = new SortedSetValue();
                _Storage.AddOrUpdate(key, sortedSetValue, (k, v) => sortedSetValue);
            }

            try
            {
                int added = sortedSetValue.ZAdd(scoreMembers);
                Console.WriteLine($"Executed: ZADD {key} -> {added}");
                return $":{added}\r\n";
            }
            catch
            {
                return "-ERR value is not a valid float\r\n";
            }
        }

        private string HandleZremCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 3)
                return "-ERR wrong number of arguments for 'zrem' command\r\n";

            var key = commandArgs[1];
            var members = commandArgs.Skip(2).ToArray();
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is SortedSetValue sortedSetValue)
                {
                    int removed = sortedSetValue.ZRem(members);
                    Console.WriteLine($"Executed: ZREM {key} -> {removed}");
                    return $":{removed}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: ZREM {key} -> 0");
                return ":0\r\n";
            }
        }

        private string HandleZscoreCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 3)
                return "-ERR wrong number of arguments for 'zscore' command\r\n";

            var key = commandArgs[1];
            var member = commandArgs[2];
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is SortedSetValue sortedSetValue)
                {
                    var score = sortedSetValue.ZScore(member);
                    if (score.HasValue)
                    {
                        Console.WriteLine($"Executed: ZSCORE {key} {member} -> {score.Value}");
                        return $"${score.Value.ToString().Length}\r\n{score.Value}\r\n";
                    }
                    else
                    {
                        Console.WriteLine($"Executed: ZSCORE {key} {member} -> (nil)");
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
                Console.WriteLine($"Executed: ZSCORE {key} {member} -> (nil)");
                return "$-1\r\n";
            }
        }

        private string HandleZcardCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'zcard' command\r\n";

            var key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is SortedSetValue sortedSetValue)
                {
                    int count = sortedSetValue.ZCard();
                    Console.WriteLine($"Executed: ZCARD {key} -> {count}");
                    return $":{count}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: ZCARD {key} -> 0");
                return ":0\r\n";
            }
        }

        private string HandleZrangeCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 4)
                return "-ERR wrong number of arguments for 'zrange' command\r\n";

            var key = commandArgs[1];
            if (!int.TryParse(commandArgs[2], out var start) || !int.TryParse(commandArgs[3], out var stop))
                return "-ERR value is not an integer or out of range\r\n";
            
            bool withScores = commandArgs.Length > 4 && commandArgs[4].ToUpper() == "WITHSCORES";
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is SortedSetValue sortedSetValue)
                {
                    var range = sortedSetValue.ZRange(start, stop, withScores);
                    var response = new StringBuilder();
                    response.Append($"*{range.Length}\r\n");
                    foreach (var item in range)
                    {
                        response.Append($"${item.Length}\r\n{item}\r\n");
                    }
                    Console.WriteLine($"Executed: ZRANGE {key} {start} {stop} -> {range.Length} elements");
                    return response.ToString();
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: ZRANGE {key} {start} {stop} -> empty list");
                return "*0\r\n";
            }
        }

        private string HandleZincrbyCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 4)
                return "-ERR wrong number of arguments for 'zincrby' command\r\n";

            var key = commandArgs[1];
            if (!double.TryParse(commandArgs[2], out var increment))
                return "-ERR value is not a valid float\r\n";
            var member = commandArgs[3];
            
            SortedSetValue sortedSetValue;
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is SortedSetValue existing)
                    sortedSetValue = existing;
                else
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
            }
            else
            {
                sortedSetValue = new SortedSetValue();
                _Storage.AddOrUpdate(key, sortedSetValue, (k, v) => sortedSetValue);
            }

            double newScore = sortedSetValue.ZIncrBy(increment, member);
            Console.WriteLine($"Executed: ZINCRBY {key} {increment} {member} -> {newScore}");
            return $"${newScore.ToString().Length}\r\n{newScore}\r\n";
        }

        // JSON Commands
        private string HandleJsonSetCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 4)
                return "-ERR wrong number of arguments for 'json.set' command\r\n";

            var key = commandArgs[1];
            var path = commandArgs[2];
            var jsonValue = commandArgs[3];
            string? condition = commandArgs.Length > 4 ? commandArgs[4] : null;
            
            JsonValue jsonValueObj;
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
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
                Console.WriteLine($"Executed: JSON.SET {key} {path} -> OK");
                return "+OK\r\n";
            }
            else
            {
                Console.WriteLine($"Executed: JSON.SET {key} {path} -> (nil)");
                return "$-1\r\n";
            }
        }

        private string HandleJsonGetCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
                return "-ERR wrong number of arguments for 'json.get' command\r\n";

            var key = commandArgs[1];
            var path = commandArgs.Length > 2 ? commandArgs[2] : ".";
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is JsonValue jsonValue)
                {
                    var result = jsonValue.JsonGet(path);
                    if (result != null)
                    {
                        Console.WriteLine($"Executed: JSON.GET {key} {path} -> {result.Length} bytes");
                        return $"${result.Length}\r\n{result}\r\n";
                    }
                    else
                    {
                        Console.WriteLine($"Executed: JSON.GET {key} {path} -> (nil)");
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
                Console.WriteLine($"Executed: JSON.GET {key} {path} -> (nil)");
                return "$-1\r\n";
            }
        }

        private string HandleJsonDelCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
                return "-ERR wrong number of arguments for 'json.del' command\r\n";

            var key = commandArgs[1];
            var path = commandArgs.Length > 2 ? commandArgs[2] : ".";
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is JsonValue jsonValue)
                {
                    int deleted = jsonValue.JsonDel(path);
                    Console.WriteLine($"Executed: JSON.DEL {key} {path} -> {deleted}");
                    return $":{deleted}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: JSON.DEL {key} {path} -> 0");
                return ":0\r\n";
            }
        }

        // Stream Commands
        private string HandleXaddCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 4 || (commandArgs.Length - 3) % 2 != 0)
                return "-ERR wrong number of arguments for 'xadd' command\r\n";

            var key = commandArgs[1];
            var id = commandArgs[2];
            var fields = commandArgs.Skip(3).ToArray();
            
            StreamValue streamValue;
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
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
                Console.WriteLine($"Executed: XADD {key} {id} -> {actualId}");
                return $"${actualId.Length}\r\n{actualId}\r\n";
            }
            catch (Exception ex)
            {
                return $"-ERR {ex.Message}\r\n";
            }
        }

        private string HandleXrangeCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 4)
                return "-ERR wrong number of arguments for 'xrange' command\r\n";

            var key = commandArgs[1];
            var start = commandArgs[2];
            var end = commandArgs[3];
            int count = commandArgs.Length > 4 && commandArgs[4].ToUpper() == "COUNT" && commandArgs.Length > 5 ? 
                (int.TryParse(commandArgs[5], out var c) ? c : -1) : -1;
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is StreamValue streamValue)
                {
                    var entries = streamValue.XRange(start, end, count);
                    var response = new StringBuilder();
                    response.Append($"*{entries.Length}\r\n");
                    foreach (var entry in entries)
                    {
                        response.Append($"*2\r\n"); // Entry array: [id, fields]
                        response.Append($"${entry.Id.Length}\r\n{entry.Id}\r\n");
                        
                        response.Append($"*{entry.Fields.Count * 2}\r\n"); // Fields array
                        foreach (var field in entry.Fields)
                        {
                            response.Append($"${field.Key.Length}\r\n{field.Key}\r\n");
                            response.Append($"${field.Value.Length}\r\n{field.Value}\r\n");
                        }
                    }
                    Console.WriteLine($"Executed: XRANGE {key} {start} {end} -> {entries.Length} entries");
                    return response.ToString();
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: XRANGE {key} {start} {end} -> empty stream");
                return "*0\r\n";
            }
        }

        private string HandleXlenCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'xlen' command\r\n";

            var key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is StreamValue streamValue)
                {
                    int length = streamValue.XLen();
                    Console.WriteLine($"Executed: XLEN {key} -> {length}");
                    return $":{length}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: XLEN {key} -> 0");
                return ":0\r\n";
            }
        }

        private string HandleXdelCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 3)
                return "-ERR wrong number of arguments for 'xdel' command\r\n";

            var key = commandArgs[1];
            var ids = commandArgs.Skip(2).ToArray();
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is StreamValue streamValue)
                {
                    int deleted = streamValue.XDel(ids);
                    Console.WriteLine($"Executed: XDEL {key} -> {deleted}");
                    return $":{deleted}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: XDEL {key} -> 0");
                return ":0\r\n";
            }
        }

        private string HandleXinfoCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2)
                return "-ERR wrong number of arguments for 'xinfo' command\r\n";

            var subcommand = commandArgs[1].ToUpper();
            
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

        private string HandleXinfoStreamCommand(string[] commandArgs)
        {
            var key = commandArgs[2];
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is StreamValue streamValue)
                {
                    var response = new StringBuilder();
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
                    
                    Console.WriteLine($"Executed: XINFO STREAM {key} -> stream info");
                    return response.ToString();
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: XINFO STREAM {key} -> no such key");
                return "-ERR no such key\r\n";
            }
        }

        private string HandleXinfoGroupsCommand(string[] commandArgs)
        {
            var key = commandArgs[2];
            
            // For simplicity, return empty array since we don't implement consumer groups
            Console.WriteLine($"Executed: XINFO GROUPS {key} -> empty");
            return "*0\r\n";
        }

        private string HandleXinfoConsumersCommand(string[] commandArgs)
        {
            var key = commandArgs[2];
            var group = commandArgs[3];
            
            // For simplicity, return empty array since we don't implement consumer groups
            Console.WriteLine($"Executed: XINFO CONSUMERS {key} {group} -> empty");
            return "*0\r\n";
        }
    }
}