namespace Sample.RedisInterface
{
    using System;
    using System.Linq;
    using System.Text;

    public partial class RedisInterfaceServer
    {
        // Set Commands
        private string HandleSaddCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 3)
                return "-ERR wrong number of arguments for 'sadd' command\r\n";

            var key = commandArgs[1];
            var values = commandArgs.Skip(2).ToArray();
            
            SetValue setValue;
            if (_Storage.TryGetValue(key, out var existingValue) && !existingValue.IsExpired)
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
            Console.WriteLine($"Executed: SADD {key} -> {added}");
            return $":{added}\r\n";
        }

        private string HandleSremCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 3)
                return "-ERR wrong number of arguments for 'srem' command\r\n";

            var key = commandArgs[1];
            var values = commandArgs.Skip(2).ToArray();
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    int removed = setValue.SRem(values);
                    Console.WriteLine($"Executed: SREM {key} -> {removed}");
                    return $":{removed}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: SREM {key} -> 0");
                return ":0\r\n";
            }
        }

        private string HandleSmembersCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'smembers' command\r\n";

            var key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    var members = setValue.SMembers();
                    var response = new StringBuilder();
                    response.Append($"*{members.Length}\r\n");
                    foreach (var member in members)
                    {
                        response.Append($"${member.Length}\r\n{member}\r\n");
                    }
                    Console.WriteLine($"Executed: SMEMBERS {key} -> {members.Length} members");
                    return response.ToString();
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: SMEMBERS {key} -> empty set");
                return "*0\r\n";
            }
        }

        private string HandleSismemberCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 3)
                return "-ERR wrong number of arguments for 'sismember' command\r\n";

            var key = commandArgs[1];
            var member = commandArgs[2];
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    bool exists = setValue.SIsMember(member);
                    Console.WriteLine($"Executed: SISMEMBER {key} {member} -> {(exists ? 1 : 0)}");
                    return $":{(exists ? 1 : 0)}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: SISMEMBER {key} {member} -> 0");
                return ":0\r\n";
            }
        }

        private string HandleScardCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'scard' command\r\n";

            var key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    int count = setValue.SCard();
                    Console.WriteLine($"Executed: SCARD {key} -> {count}");
                    return $":{count}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: SCARD {key} -> 0");
                return ":0\r\n";
            }
        }

        private string HandleSpopCommand(string[] commandArgs)
        {
            if (commandArgs.Length != 2)
                return "-ERR wrong number of arguments for 'spop' command\r\n";

            var key = commandArgs[1];
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    var popped = setValue.SPop();
                    if (popped == null)
                    {
                        Console.WriteLine($"Executed: SPOP {key} -> (nil)");
                        return "$-1\r\n";
                    }
                    Console.WriteLine($"Executed: SPOP {key} -> {popped}");
                    return $"${popped.Length}\r\n{popped}\r\n";
                }
                else
                {
                    return "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
                }
            }
            else
            {
                Console.WriteLine($"Executed: SPOP {key} -> (nil)");
                return "$-1\r\n";
            }
        }

        private string HandleSrandmemberCommand(string[] commandArgs)
        {
            if (commandArgs.Length < 2 || commandArgs.Length > 3)
                return "-ERR wrong number of arguments for 'srandmember' command\r\n";

            var key = commandArgs[1];
            int count = 1;
            if (commandArgs.Length == 3 && !int.TryParse(commandArgs[2], out count))
                return "-ERR value is not an integer or out of range\r\n";
            
            if (_Storage.TryGetValue(key, out var value) && !value.IsExpired)
            {
                if (value is SetValue setValue)
                {
                    var members = setValue.SRandMember(count);
                    if (commandArgs.Length == 2)
                    {
                        // Single member
                        if (members.Length == 0)
                        {
                            Console.WriteLine($"Executed: SRANDMEMBER {key} -> (nil)");
                            return "$-1\r\n";
                        }
                        Console.WriteLine($"Executed: SRANDMEMBER {key} -> {members[0]}");
                        return $"${members[0].Length}\r\n{members[0]}\r\n";
                    }
                    else
                    {
                        // Multiple members
                        var response = new StringBuilder();
                        response.Append($"*{members.Length}\r\n");
                        foreach (var member in members)
                        {
                            response.Append($"${member.Length}\r\n{member}\r\n");
                        }
                        Console.WriteLine($"Executed: SRANDMEMBER {key} {count} -> {members.Length} members");
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
                    Console.WriteLine($"Executed: SRANDMEMBER {key} -> (nil)");
                    return "$-1\r\n";
                }
                else
                {
                    Console.WriteLine($"Executed: SRANDMEMBER {key} {count} -> empty list");
                    return "*0\r\n";
                }
            }
        }
    }
}