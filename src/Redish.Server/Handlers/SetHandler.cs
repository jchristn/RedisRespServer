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
    /// Handles Redis Set data type commands including SADD, SREM, SMEMBERS, SISMEMBER, SCARD, SPOP, and SRANDMEMBER.
    /// </summary>
    /// <remarks>
    /// This class provides implementations for all Redis Set operations, managing the storage and retrieval
    /// of set values with proper error handling and RESP protocol formatting.
    /// </remarks>
    public class SetHandler
    {
        private readonly StorageBase _Storage;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[SetHandler]";

        /// <summary>
        /// Initializes a new instance of the <see cref="SetHandler"/> class.
        /// </summary>
        /// <param name="logging">The logging instance for operation tracking.</param>
        /// <param name="storage">The storage instance for Redis values.</param>
        /// <exception cref="ArgumentNullException">Thrown when storage or logging is null.</exception>
        public SetHandler(LoggingModule logging, StorageBase storage)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// Handles the SADD command to add members to a set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key and values to add.</param>
        /// <returns>A RESP-formatted response indicating the number of members added.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleSaddCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 3)
                return "-ERR wrong number of arguments for 'sadd' command\r\n";

            string key = commandArgs[1];
            string[] values = commandArgs.Skip(2).ToArray();
            
            SetValue setValue;
            if (_Storage.TryGetValue(key, out RedisValue existingValue) && !existingValue.IsExpired)
            {
                if (existingValue is SetValue existing)
                    setValue = existing;
                else
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
            }
            else
            {
                setValue = new SetValue();
                _Storage.AddOrUpdate(key, setValue, (k, v) => setValue);
            }

            int added = setValue.SAdd(values);
            _Logging.Debug(_Header + $"Executed: SADD {key} -> {added}");
            return $":{added}\r\n";
        }

        /// <summary>
        /// Handles the SREM command to remove members from a set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key and values to remove.</param>
        /// <returns>A RESP-formatted response indicating the number of members removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleSremCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 3)
                return "-ERR wrong number of arguments for 'srem' command\r\n";

            string key = commandArgs[1];
            string[] values = commandArgs.Skip(2).ToArray();
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    int removed = setValue.SRem(values);
                    _Logging.Debug(_Header + $"Executed: SREM {key} -> {removed}");
                    return $":{removed}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: SREM {key} -> 0");
                return ":0\r\n";
            }
        }

        /// <summary>
        /// Handles the SMEMBERS command to get all members of a set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key.</param>
        /// <returns>A RESP-formatted array of all set members.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleSmembersCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'smembers' command\r\n";

            string key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    string[] members = setValue.SMembers();
                    StringBuilder response = new StringBuilder();
                    response.Append($"*{members.Length}\r\n");
                    foreach (string member in members)
                    {
                        response.Append($"${member.Length}\r\n{member}\r\n");
                    }
                    _Logging.Debug(_Header + $"Executed: SMEMBERS {key} -> {members.Length} members");
                    return response.ToString();
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: SMEMBERS {key} -> empty set");
                return "*0\r\n";
            }
        }

        /// <summary>
        /// Handles the SISMEMBER command to check if a value is a member of a set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key and member to check.</param>
        /// <returns>A RESP-formatted response indicating 1 if member exists, 0 otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleSismemberCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length != 3)
                return "-ERR wrong number of arguments for 'sismember' command\r\n";

            string key = commandArgs[1];
            string member = commandArgs[2];
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    bool exists = setValue.SIsMember(member);
                    _Logging.Debug(_Header + $"Executed: SISMEMBER {key} {member} -> {(exists ? 1 : 0)}");
                    return $":{(exists ? 1 : 0)}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: SISMEMBER {key} {member} -> 0");
                return ":0\r\n";
            }
        }

        /// <summary>
        /// Handles the SCARD command to get the cardinality (number of elements) of a set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key.</param>
        /// <returns>A RESP-formatted response with the number of elements in the set.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleScardCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'scard' command\r\n";

            string key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    int count = setValue.SCard();
                    _Logging.Debug(_Header + $"Executed: SCARD {key} -> {count}");
                    return $":{count}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: SCARD {key} -> 0");
                return ":0\r\n";
            }
        }

        /// <summary>
        /// Handles the SPOP command to remove and return a random member from a set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key.</param>
        /// <returns>A RESP-formatted response with the removed member, or nil if empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleSpopCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'spop' command\r\n";

            string key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    string? popped = setValue.SPop();
                    if (popped == null)
                    {
                        _Logging.Debug(_Header + $"Executed: SPOP {key} -> (nil)");
                        return "$-1\r\n";
                    }
                    _Logging.Debug(_Header + $"Executed: SPOP {key} -> {popped}");
                    return $"${popped.Length}\r\n{popped}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                _Logging.Debug(_Header + $"Executed: SPOP {key} -> (nil)");
                return "$-1\r\n";
            }
        }

        /// <summary>
        /// Handles the SRANDMEMBER command to get random members from a set.
        /// </summary>
        /// <param name="commandArgs">The command arguments including the key and optional count.</param>
        /// <returns>A RESP-formatted response with random members from the set.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandArgs is null.</exception>
        public string HandleSrandmemberCommand(string[] commandArgs)
        {
            if (commandArgs == null)
                throw new ArgumentNullException(nameof(commandArgs));

            if (commandArgs.Length < 2 || commandArgs.Length > 3)
                return "-ERR wrong number of arguments for 'srandmember' command\r\n";

            string key = commandArgs[1];
            int count = 1;
            if (commandArgs.Length == 3 && !int.TryParse(commandArgs[2], out count))
                return "-ERR value is not an integer or out of range\r\n";
            
            if (_Storage.TryGetValue(key, out RedisValue value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    string[] members = setValue.SRandMember(count);
                    if (commandArgs.Length == 2)
                    {
                        // Single member
                        if (members.Length == 0)
                        {
                            _Logging.Debug(_Header + $"Executed: SRANDMEMBER {key} -> (nil)");
                            return "$-1\r\n";
                        }
                        _Logging.Debug(_Header + $"Executed: SRANDMEMBER {key} -> {members[0]}");
                        return $"${members[0].Length}\r\n{members[0]}\r\n";
                    }
                    else
                    {
                        // Multiple members
                        StringBuilder response = new StringBuilder();
                        response.Append($"*{members.Length}\r\n");
                        foreach (string member in members)
                        {
                            response.Append($"${member.Length}\r\n{member}\r\n");
                        }
                        _Logging.Debug(_Header + $"Executed: SRANDMEMBER {key} {count} -> {members.Length} members");
                        return response.ToString();
                    }
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                if (commandArgs.Length == 2)
                {
                    _Logging.Debug(_Header + $"Executed: SRANDMEMBER {key} -> (nil)");
                    return "$-1\r\n";
                }
                else
                {
                    _Logging.Debug(_Header + $"Executed: SRANDMEMBER {key} {count} -> empty list");
                    return "*0\r\n";
                }
            }
        }
    }
}