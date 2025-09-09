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
    /// Handles Redis Sorted Set data type commands including ZADD, ZREM, ZSCORE, ZCARD, ZRANGE, and ZINCRBY.
    /// </summary>
    /// <remarks>
    /// This class provides implementations for all Redis Sorted Set operations, managing the storage and retrieval
    /// of sorted set values with proper error handling and RESP protocol formatting.
    /// </remarks>
    public class SortedSetHandler
    {
        private readonly StorageBase _Storage;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[SortedSetHandler]";

        /// <summary>
        /// Initializes a new instance of the <see cref="SortedSetHandler"/> class.
        /// </summary>
        /// <param name="logging">The logging instance for operation tracking.</param>
        /// <param name="storage">The storage instance for Redis values.</param>
        /// <exception cref="ArgumentNullException">Thrown when storage or logging is null.</exception>
        public SortedSetHandler(LoggingModule logging, StorageBase storage)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// Handles the ZADD command to add members with scores to a sorted set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key, scores, and members to add.</param>
        /// <returns>A RESP-formatted response indicating the number of members added.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleZaddCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 4 || (commandArgs.Length - 2) % 2 != 0)
                return "-ERR wrong number of arguments for 'zadd' command\r\n";

            string key = commandArgs[1];
            object[] scoreMembers = commandArgs.Skip(2).Cast<object>().ToArray();
            
            SortedSetValue sortedSetValue;
            if (_Storage.TryGetValue(key, out RedisValue existingValue) && !existingValue.IsExpired)
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
                _Logging.Debug(_Header + $"Executed: ZADD {key} -> {added}");
                return $":{added}\r\n";
            }
            catch
            {
                return "-ERR value is not a valid float\r\n";
            }
        }

        /// <summary>
        /// Handles the ZREM command to remove members from a sorted set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key and members to remove.</param>
        /// <returns>A RESP-formatted response indicating the number of members removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleZremCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 3)
                return "-ERR wrong number of arguments for 'zrem' command\r\n";

            string key = commandArgs[1];
            string[] members = commandArgs.Skip(2).ToArray();
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is SortedSetValue sortedSetValue)
                {
                    int removed = sortedSetValue.ZRem(members);
                    _Logging.Debug(_Header + $"Executed: ZREM {key} -> {removed}");
                    return $":{removed}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: ZREM {key} -> 0");
                return ":0\r\n";
            }
        }

        /// <summary>
        /// Handles the ZSCORE command to get the score of a member in a sorted set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key and member.</param>
        /// <returns>A RESP-formatted response with the member's score or nil if not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleZscoreCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length != 3)
                return "-ERR wrong number of arguments for 'zscore' command\r\n";

            string key = commandArgs[1];
            string member = commandArgs[2];
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is SortedSetValue sortedSetValue)
                {
                    double? score = sortedSetValue.ZScore(member);
                    if (score.HasValue)
                    {
                        _Logging.Debug(_Header + $"Executed: ZSCORE {key} {member} -> {score.Value}");
                        return $"${score.Value.ToString().Length}\r\n{score.Value}\r\n";
                    }
                    else
                    {
                        _Logging.Debug(_Header + $"Executed: ZSCORE {key} {member} -> (nil)");
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
                _Logging.Debug(_Header + $"Executed: ZSCORE {key} {member} -> (nil)");
                return "$-1\r\n";
            }
        }

        /// <summary>
        /// Handles the ZCARD command to get the cardinality (number of elements) of a sorted set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key.</param>
        /// <returns>A RESP-formatted response with the number of elements in the sorted set.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleZcardCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'zcard' command\r\n";

            string key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is SortedSetValue sortedSetValue)
                {
                    int count = sortedSetValue.ZCard();
                    _Logging.Debug(_Header + $"Executed: ZCARD {key} -> {count}");
                    return $":{count}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: ZCARD {key} -> 0");
                return ":0\r\n";
            }
        }

        /// <summary>
        /// Handles the ZRANGE command to get members from a sorted set by rank range.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key, start, stop, and optional WITHSCORES.</param>
        /// <returns>A RESP-formatted response with the members in the specified range.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleZrangeCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 4)
                return "-ERR wrong number of arguments for 'zrange' command\r\n";

            string key = commandArgs[1];
            if (!int.TryParse(commandArgs[2], out int start) || !int.TryParse(commandArgs[3], out int stop))
                return "-ERR value is not an integer or out of range\r\n";
            
            bool withScores = commandArgs.Length > 4 && commandArgs[4].ToUpper() == "WITHSCORES";

            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is SortedSetValue sortedSetValue)
                {
                    string[] range = sortedSetValue.ZRange(start, stop, withScores);
                    StringBuilder response = new StringBuilder();
                    response.Append($"*{range.Length}\r\n");
                    foreach (string item in range)
                    {
                        response.Append($"${item.Length}\r\n{item}\r\n");
                    }
                    _Logging.Debug(_Header + $"Executed: ZRANGE {key} {start} {stop} -> {range.Length} elements");
                    return response.ToString();
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: ZRANGE {key} {start} {stop} -> empty list");
                return "*0\r\n";
            }
        }

        /// <summary>
        /// Handles the ZINCRBY command to increment the score of a member in a sorted set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key, increment, and member.</param>
        /// <returns>A RESP-formatted response with the new score of the member.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleZincrbyCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length != 4)
                return "-ERR wrong number of arguments for 'zincrby' command\r\n";

            string key = commandArgs[1];
            if (!double.TryParse(commandArgs[2], out double increment))
                return "-ERR value is not a valid float\r\n";
            string member = commandArgs[3];
            
            SortedSetValue sortedSetValue;
            if (_Storage.TryGetValue(key, out RedisValue existingValue) && !existingValue.IsExpired)
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
            _Logging.Debug(_Header + $"Executed: ZINCRBY {key} {increment} {member} -> {newScore}");
            return $"${newScore.ToString().Length}\r\n{newScore}\r\n";
        }
    }
}