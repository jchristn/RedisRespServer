namespace Test.StackExchangeRedis
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using StackExchange.Redis;

    /// <summary>
    /// Entry point for the Test StackExchange.Redis Client application.
    /// </summary>
    /// <remarks>
    /// This console application provides an interactive Redis client using StackExchange.Redis
    /// that can connect to any Redis server and execute persistence-related commands.
    /// It serves to validate compatibility of RedisListener and Sample.RedisServer with
    /// existing Redis client libraries.
    /// </remarks>
    public class Program
    {
        private static readonly bool _Logging = false;

        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        /// <param name="args">Command line arguments. Hostname and port are required.</param>
        /// <returns>A task representing the asynchronous program execution.</returns>
        /// <remarks>
        /// The application requires hostname and port as command line arguments.
        /// Usage: Test.StackExchangeRedis.exe &lt;hostname&gt; &lt;port&gt; [--test]
        /// Example: Test.StackExchangeRedis.exe localhost 6379
        /// Example: Test.StackExchangeRedis.exe localhost 6379 --test
        /// </remarks>
        public static async Task Main(string[] args)
        {
            // Default to automated test mode on localhost:6380 if no args provided
            if (args.Length == 0)
            {
                Console.WriteLine("=== Test StackExchange.Redis Client ===");
                Console.WriteLine("No arguments provided - running automated test suite on localhost:6380");
                Console.WriteLine();
                
                await RunAutomatedTestSuiteAsync("localhost", 6379, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            
            if (args.Length < 2)
            {
                Usage();
                return;
            }

            string host = args[0];
            
            if (!int.TryParse(args[1], out int port) || port <= 0 || port > 65535)
            {
                Console.WriteLine("Error: Port must be a valid number between 1 and 65535.");
                Usage();
                return;
            }

            bool runTests = args.Length > 2 && args[2] == "--test";

            Console.WriteLine("=== Test StackExchange.Redis Client ===");
            Console.WriteLine($"Connecting to Redis server at {host}:{port}");
            Console.WriteLine();

            if (runTests)
            {
                await RunAutomatedTestSuiteAsync(host, port, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                await RunInteractiveSessionAsync(host, port, CancellationToken.None).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Displays usage information for the application.
        /// </summary>
        /// <remarks>
        /// Shows the correct command line syntax and provides usage examples.
        /// </remarks>
        public static void Usage()
        {
            Console.WriteLine("Test StackExchange.Redis Client");
            Console.WriteLine("Usage: Test.StackExchangeRedis.exe <hostname> <port> [--test]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  hostname    The Redis server hostname or IP address");
            Console.WriteLine("  port        The Redis server port number (1-65535)");
            Console.WriteLine("  --test      Run automated test suite instead of interactive mode");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  Test.StackExchangeRedis.exe localhost 6379");
            Console.WriteLine("  Test.StackExchangeRedis.exe localhost 6379 --test");
            Console.WriteLine("  Test.StackExchangeRedis.exe 192.168.1.100 6380");
            Console.WriteLine("  Test.StackExchangeRedis.exe redis.example.com 6379 --test");
        }

        private static async Task RunInteractiveSessionAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            ConnectionMultiplexer? connection = null;
            IDatabase? database = null;

            try
            {
                Console.WriteLine("Creating connection configuration...");
                var configurationOptions = new ConfigurationOptions
                {
                    EndPoints = { $"{host}:{port}" },
                    ConnectTimeout = 15000,
                    ConnectRetry = 5,
                    AbortOnConnectFail = false,
                    CheckCertificateRevocation = false,
                    AllowAdmin = true
                };
                
                // Enable detailed logging if debug is enabled
                var logger = new StringWriter();
                if (_Logging)
                {
                    configurationOptions.LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
                    {
                        builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
                    });
                }
                
                Console.WriteLine("Attempting to connect...");
                connection = await ConnectionMultiplexer.ConnectAsync(configurationOptions, logger).ConfigureAwait(false);
                database = connection.GetDatabase();
                
                Console.WriteLine("Connected successfully!");
                ShowCommandMenu();

                while (!cancellationToken.IsCancellationRequested)
                {
                    Console.Write("Command [?/help]: ");
                    var input = Console.ReadLine()?.Trim();
                    
                    if (string.IsNullOrEmpty(input)) continue;
                    
                    if (input.Equals("quit", StringComparison.OrdinalIgnoreCase) || 
                        input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (input.Equals("?"))
                    {
                        ShowCommandMenu();
                        continue;
                    }

                    try
                    {
                        await ProcessCommandAsync(database, input, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error executing command: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
            }
            finally
            {
                if (connection != null)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                    connection.Dispose();
                    Console.WriteLine("Disconnected from Redis server.");
                }
            }
        }

        private static void ShowCommandMenu()
        {
            Console.WriteLine();
            Console.WriteLine("=== Redis Command Menu (StackExchange.Redis) ===");
            Console.WriteLine();
            Console.WriteLine("Basic Commands:");
            Console.WriteLine("  SET key value          Set a key to a value, e.g. SET mykey \"hello world\"");
            Console.WriteLine("  GET key                Get the value of a key, e.g. GET mykey");
            Console.WriteLine();
            Console.WriteLine("Other Commands:");
            Console.WriteLine("  DEL key                Delete a key, e.g. DEL mykey");
            Console.WriteLine("  EXISTS key             Check if a key exists, e.g. EXISTS mykey");
            Console.WriteLine("  KEYS pattern           List keys matching pattern (* for all), e.g. KEYS *");
            Console.WriteLine("  PING                   Test server connectivity");
            Console.WriteLine("  FLUSHDB                Clear all keys from database");
            Console.WriteLine();
            Console.WriteLine("Control Commands:");
            Console.WriteLine("  ?                      Show this command menu");
            Console.WriteLine("  quit | exit            Disconnect and exit");
            Console.WriteLine();
            Console.WriteLine("=============================");
            Console.WriteLine();
        }

        private static async Task ProcessCommandAsync(IDatabase database, string input, CancellationToken cancellationToken = default)
        {
            var parts = ParseCommand(input);
            if (parts.Length == 0) return;

            var command = parts[0].ToUpperInvariant();

            switch (command)
            {
                case "SET":
                    if (parts.Length >= 3)
                    {
                        var key = parts[1];
                        var value = string.Join(" ", parts[2..]);
                        // Remove quotes if present
                        if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length > 1)
                        {
                            value = value[1..^1];
                        }
                        
                        var setResult = await database.StringSetAsync(key, value).ConfigureAwait(false);
                        Console.WriteLine($"Response: {(setResult ? "OK" : "FAILED")}");
                    }
                    else
                    {
                        Console.WriteLine("Usage: SET key value");
                        Console.WriteLine("Example: SET mykey \"hello world\"");
                    }
                    break;

                case "GET":
                    if (parts.Length >= 2)
                    {
                        var key = parts[1];
                        var value = await database.StringGetAsync(key).ConfigureAwait(false);
                        if (value.HasValue)
                        {
                            Console.WriteLine($"Response: (string) \"{value}\"");
                        }
                        else
                        {
                            Console.WriteLine("Response: (nil)");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Usage: GET key");
                        Console.WriteLine("Example: GET mykey");
                    }
                    break;

                case "DEL":
                    if (parts.Length >= 2)
                    {
                        var key = parts[1];
                        var deleteResult = await database.KeyDeleteAsync(key).ConfigureAwait(false);
                        Console.WriteLine($"Response: (integer) {(deleteResult ? 1 : 0)}");
                    }
                    else
                    {
                        Console.WriteLine("Usage: DEL key");
                        Console.WriteLine("Example: DEL mykey");
                    }
                    break;

                case "EXISTS":
                    if (parts.Length >= 2)
                    {
                        var key = parts[1];
                        var existsResult = await database.KeyExistsAsync(key).ConfigureAwait(false);
                        Console.WriteLine($"Response: (integer) {(existsResult ? 1 : 0)}");
                    }
                    else
                    {
                        Console.WriteLine("Usage: EXISTS key");
                        Console.WriteLine("Example: EXISTS mykey");
                    }
                    break;

                case "KEYS":
                    var pattern = parts.Length >= 2 ? parts[1] : "*";
                    try
                    {
                        var server = database.Multiplexer.GetServer(database.Multiplexer.GetEndPoints().First());
                        var keys = server.Keys(pattern: pattern);
                        var keyList = keys.ToArray();
                        
                        if (keyList.Length == 0)
                        {
                            Console.WriteLine("Response: (empty list or set)");
                        }
                        else
                        {
                            Console.WriteLine("Response:");
                            for (int i = 0; i < keyList.Length; i++)
                            {
                                Console.WriteLine($"{i + 1}) \"{keyList[i]}\"");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Response: (error) {ex.Message}");
                    }
                    break;

                case "PING":
                    try
                    {
                        var pingResult = await database.PingAsync().ConfigureAwait(false);
                        Console.WriteLine($"Response: PONG ({pingResult.TotalMilliseconds:F2}ms)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Response: (error) {ex.Message}");
                    }
                    break;

                case "FLUSHDB":
                    Console.Write("Are you sure you want to delete all keys? Type 'YES' to confirm: ");
                    var confirmation = Console.ReadLine()?.Trim();
                    if (confirmation == "YES")
                    {
                        try
                        {
                            var server = database.Multiplexer.GetServer(database.Multiplexer.GetEndPoints().First());
                            await server.FlushDatabaseAsync().ConfigureAwait(false);
                            Console.WriteLine("Response: OK");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Response: (error) {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("FLUSHDB cancelled");
                    }
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Type 'help' or 'menu' to see available commands");
                    break;
            }
        }

        private static string[] ParseCommand(string input)
        {
            var parts = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            bool escapeNext = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (escapeNext)
                {
                    current.Append(c);
                    escapeNext = false;
                    continue;
                }

                if (c == '\\')
                {
                    escapeNext = true;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                parts.Add(current.ToString());
            }

            return parts.ToArray();
        }

        private static async Task RunAutomatedTestSuiteAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            var testResults = new List<TestResult>();
            var overallStopwatch = Stopwatch.StartNew();
            
            ConnectionMultiplexer? connection = null;
            IDatabase? database = null;
            Process? serverProcess = null;

            try
            {
                Console.WriteLine("Redis StackExchange Client Test Suite");
                Console.WriteLine("=" + new string('=', 59));
                
                // Start Redis server if needed
                if (host.ToLower() == "localhost" && port != 6379)
                {
                    serverProcess = await StartRedisServerAsync(port);
                    if (serverProcess != null)
                    {
                        Console.WriteLine($"✓ Started Redis server on port {port}");
                        await Task.Delay(2000); // Give server time to start
                    }
                }

                Console.WriteLine($"Target Server: {host}:{port}");
                Console.WriteLine($"Start Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine(">> Starting Redis Data Type Tests");
                Console.WriteLine();

                // Connect to Redis server
                var configurationOptions = new ConfigurationOptions
                {
                    EndPoints = { $"{host}:{port}" },
                    ConnectTimeout = 15000,
                    ConnectRetry = 5,
                    AbortOnConnectFail = false,
                    AllowAdmin = true
                };

                connection = await ConnectionMultiplexer.ConnectAsync(configurationOptions).ConfigureAwait(false);
                database = connection.GetDatabase();
                var server = connection.GetServer(connection.GetEndPoints().First());

                Console.WriteLine("✓ Connected to Redis server successfully");
                Console.WriteLine();

                // Clean database before starting tests
                await server.FlushDatabaseAsync().ConfigureAwait(false);

                // Execute all test categories
                await ExecuteTestCategory(testResults, "String Operations", () => TestStringOperations(database));
                await ExecuteTestCategory(testResults, "Hash Operations", () => TestHashOperations(database));
                await ExecuteTestCategory(testResults, "List Operations", () => TestListOperations(database));
                await ExecuteTestCategory(testResults, "Set Operations", () => TestSetOperations(database));
                await ExecuteTestCategory(testResults, "Sorted Set Operations", () => TestSortedSetOperations(database));
                await ExecuteTestCategory(testResults, "Key Management", () => TestKeyManagement(database));
                await ExecuteTestCategory(testResults, "Connection & Server", () => TestConnectionAndServer(database, server));
                await ExecuteTestCategory(testResults, "TTL & Expiration", () => TestTtlAndExpiration(database));
                await ExecuteTestCategory(testResults, "Error Conditions", () => TestErrorConditions(database));
                await ExecuteTestCategory(testResults, "Performance & Stress", () => TestPerformanceAndStress(database));

                overallStopwatch.Stop();
                DisplayTestSummary(testResults, overallStopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Test suite failed to initialize: {ex.Message}");
                overallStopwatch.Stop();
                DisplayTestSummary(testResults, overallStopwatch.Elapsed);
            }
            finally
            {
                if (connection != null)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                    connection.Dispose();
                }
                
                if (serverProcess != null && !serverProcess.HasExited)
                {
                    Console.WriteLine("Stopping Redis server...");
                    try
                    {
                        serverProcess.Kill();
                        serverProcess.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not stop Redis server: {ex.Message}");
                    }
                }
            }
        }

        private static async Task ExecuteTestCategory(List<TestResult> testResults, string categoryName, Func<Task> testMethod)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine($"TEST CATEGORY: {categoryName.ToUpper()}");
            Console.WriteLine(new string('=', 60));
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await testMethod().ConfigureAwait(false);
                stopwatch.Stop();
                
                var result = new TestResult(categoryName, true, null, stopwatch.Elapsed);
                testResults.Add(result);
                
                Console.WriteLine($"\n✅ [PASS] {categoryName} completed successfully ({stopwatch.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                var result = new TestResult(categoryName, false, ex, stopwatch.Elapsed);
                testResults.Add(result);
                
                Console.WriteLine($"\n❌ [FAIL] {categoryName} failed: {ex.Message} ({stopwatch.ElapsedMilliseconds}ms)");
            }
        }

        private static async Task<Process?> StartRedisServerAsync(int port)
        {
            try
            {
                // Try to start Sample.RedisInterface server
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project ../Sample.RedisInterface {port}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = Process.Start(startInfo);
                
                // Give it a moment to start
                await Task.Delay(1000);
                
                if (process != null && !process.HasExited)
                {
                    return process;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not start Redis server: {ex.Message}");
            }

            return null;
        }

        private static async Task TestStringOperations(IDatabase database)
        {
            Console.WriteLine(">> Testing Redis string data type operations");
            Console.WriteLine("   Expected: SET/GET, increment/decrement, multiple operations work correctly");
            Console.WriteLine("   Tests: Basic strings, special chars, numeric operations, bulk operations");
            
            // Basic string operations
            await database.StringSetAsync("str:basic", "hello world");
            var value = await database.StringGetAsync("str:basic");
            if (value != "hello world") throw new Exception("String SET/GET failed");

            // String with special characters
            await database.StringSetAsync("str:special", "áéíóú ñü 中文 🚀");
            value = await database.StringGetAsync("str:special");
            if (!value.HasValue) throw new Exception("String with special characters failed");

            // String increment/decrement
            await database.StringSetAsync("str:number", "10");
            var increment = await database.StringIncrementAsync("str:number", 5);
            if (increment != 15) throw new Exception("String increment failed");

            var decrement = await database.StringDecrementAsync("str:number", 3);
            if (decrement != 12) throw new Exception("String decrement failed");

            // Multiple string operations
            var keyValuePairs = new KeyValuePair<RedisKey, RedisValue>[]
            {
                new("str:multi1", "value1"),
                new("str:multi2", "value2"),
                new("str:multi3", "value3")
            };
            
            var setResult = await database.StringSetAsync(keyValuePairs);
            if (!setResult) throw new Exception("Multiple string SET failed");

            var getResult = await database.StringGetAsync(new RedisKey[] { "str:multi1", "str:multi2", "str:multi3" });
            if (getResult.Length != 3 || getResult[0] != "value1") throw new Exception("Multiple string GET failed");
        }

        private static async Task TestHashOperations(IDatabase database)
        {
            Console.WriteLine(">> Testing Redis hash data type operations");
            Console.WriteLine("   Expected: HSET/HGET/HGETALL, field management work correctly");
            Console.WriteLine("   Tests: Single fields, multiple fields, exists/delete, length");
            
            // Basic hash operations
            await database.HashSetAsync("hash:user", "name", "John Doe");
            await database.HashSetAsync("hash:user", "email", "john@example.com");
            await database.HashSetAsync("hash:user", "age", "30");

            var name = await database.HashGetAsync("hash:user", "name");
            if (name != "John Doe") throw new Exception("Hash HSET/HGET failed");

            // Hash multiple set
            var hashFields = new HashEntry[]
            {
                new("city", "New York"),
                new("country", "USA"),
                new("zip", "10001")
            };
            
            await database.HashSetAsync("hash:user", hashFields);
            
            var allFields = await database.HashGetAllAsync("hash:user");
            if (allFields.Length < 6) throw new Exception("Hash HMSET/HGETALL failed");

            // Hash exists and delete
            var exists = await database.HashExistsAsync("hash:user", "name");
            if (!exists) throw new Exception("Hash HEXISTS failed");

            var deleted = await database.HashDeleteAsync("hash:user", "age");
            if (!deleted) throw new Exception("Hash HDEL failed");

            // Hash length
            var length = await database.HashLengthAsync("hash:user");
            if (length == 0) throw new Exception("Hash HLEN failed");
        }

        private static async Task TestListOperations(IDatabase database)
        {
            Console.WriteLine(">> Testing Redis list data type operations");
            Console.WriteLine("   Expected: PUSH/POP operations, indexing, range queries work correctly");
            Console.WriteLine("   Tests: Left/right push, pop operations, range access, indexing");
            
            // Right push operations
            await database.ListRightPushAsync("list:numbers", new RedisValue[] { "1", "2", "3" });
            var length = await database.ListLengthAsync("list:numbers");
            if (length != 3) throw new Exception("List RPUSH failed");

            // Left push operations
            await database.ListLeftPushAsync("list:numbers", "0");
            length = await database.ListLengthAsync("list:numbers");
            if (length != 4) throw new Exception("List LPUSH failed");

            // List range
            var range = await database.ListRangeAsync("list:numbers", 0, -1);
            if (range.Length != 4 || range[0] != "0" || range[3] != "3") throw new Exception("List LRANGE failed");

            // Pop operations
            var leftPop = await database.ListLeftPopAsync("list:numbers");
            if (leftPop != "0") throw new Exception("List LPOP failed");

            var rightPop = await database.ListRightPopAsync("list:numbers");
            if (rightPop != "3") throw new Exception("List RPOP failed");

            // List index and set
            var index = await database.ListGetByIndexAsync("list:numbers", 0);
            if (index != "1") throw new Exception("List LINDEX failed");

            await database.ListSetByIndexAsync("list:numbers", 0, "10");
            index = await database.ListGetByIndexAsync("list:numbers", 0);
            if (index != "10") throw new Exception("List LSET failed");
        }

        private static async Task TestSetOperations(IDatabase database)
        {
            Console.WriteLine(">> Testing Redis set data type operations");
            Console.WriteLine("   Expected: SADD/SREM, membership tests, set operations work correctly");
            Console.WriteLine("   Tests: Add/remove members, membership, random/pop, set combinations");
            
            // Add members to set
            await database.SetAddAsync("set:colors", new RedisValue[] { "red", "green", "blue" });
            var length = await database.SetLengthAsync("set:colors");
            if (length != 3) throw new Exception("Set SADD failed");

            // Check membership
            var isMember = await database.SetContainsAsync("set:colors", "red");
            if (!isMember) throw new Exception("Set SISMEMBER failed");

            // Get all members
            var members = await database.SetMembersAsync("set:colors");
            if (members.Length != 3) throw new Exception("Set SMEMBERS failed");

            // Remove member
            var removed = await database.SetRemoveAsync("set:colors", "blue");
            if (!removed) throw new Exception("Set SREM failed");

            // Random member
            var randomMember = await database.SetRandomMemberAsync("set:colors");
            if (!randomMember.HasValue) throw new Exception("Set SRANDMEMBER failed");

            // Pop member
            var poppedMember = await database.SetPopAsync("set:colors");
            if (!poppedMember.HasValue) throw new Exception("Set SPOP failed");

            // Set operations with another set
            await database.SetAddAsync("set:primary", new RedisValue[] { "red", "yellow", "blue" });
            await database.SetAddAsync("set:secondary", new RedisValue[] { "green", "orange", "purple" });

            var union = await database.SetCombineAsync(SetOperation.Union, "set:primary", "set:secondary");
            if (union.Length != 6) throw new Exception("Set SUNION failed");
        }

        private static async Task TestSortedSetOperations(IDatabase database)
        {
            Console.WriteLine(">> Testing Redis sorted set data type operations");
            Console.WriteLine("   Expected: ZADD/ZREM with scores, range queries work correctly");
            Console.WriteLine("   Tests: Scored members, score queries, rank/range operations, increments");
            
            // Add scored members
            await database.SortedSetAddAsync("zset:scores", new SortedSetEntry[] 
            {
                new("Alice", 100),
                new("Bob", 85),
                new("Charlie", 92),
                new("Diana", 88)
            });

            var length = await database.SortedSetLengthAsync("zset:scores");
            if (length != 4) throw new Exception("Sorted Set ZADD failed");

            // Get score
            var score = await database.SortedSetScoreAsync("zset:scores", "Alice");
            if (!score.HasValue || score != 100) throw new Exception("Sorted Set ZSCORE failed");

            // Range by rank
            var range = await database.SortedSetRangeByRankAsync("zset:scores", 0, -1, Order.Descending);
            if (range.Length != 4 || range[0] != "Alice") throw new Exception("Sorted Set ZRANGE failed");

            // Range by score
            var scoreRange = await database.SortedSetRangeByScoreAsync("zset:scores", 85, 95);
            if (scoreRange.Length != 3) throw new Exception("Sorted Set ZRANGEBYSCORE failed");

            // Increment score
            var newScore = await database.SortedSetIncrementAsync("zset:scores", "Bob", 10);
            if (newScore != 95) throw new Exception("Sorted Set ZINCRBY failed");

            // Remove member
            var removed = await database.SortedSetRemoveAsync("zset:scores", "Charlie");
            if (!removed) throw new Exception("Sorted Set ZREM failed");

            // Rank
            var rank = await database.SortedSetRankAsync("zset:scores", "Alice", Order.Descending);
            if (rank != 0) throw new Exception("Sorted Set ZRANK failed");
        }

        private static async Task TestKeyManagement(IDatabase database)
        {
            // Set keys for testing
            await database.StringSetAsync("key:test1", "value1");
            await database.StringSetAsync("key:test2", "value2");
            await database.HashSetAsync("key:hash", "field", "value");

            // Key exists
            var exists = await database.KeyExistsAsync("key:test1");
            if (!exists) throw new Exception("Key EXISTS failed");

            // Key type
            var type = await database.KeyTypeAsync("key:test1");
            if (type != RedisType.String) throw new Exception("Key TYPE failed");

            type = await database.KeyTypeAsync("key:hash");
            if (type != RedisType.Hash) throw new Exception("Key TYPE for hash failed");

            // Delete key
            var deleted = await database.KeyDeleteAsync("key:test1");
            if (!deleted) throw new Exception("Key DEL failed");

            // Multiple key delete
            var deletedCount = await database.KeyDeleteAsync(new RedisKey[] { "key:test2", "key:hash" });
            if (deletedCount != 2) throw new Exception("Multiple key DEL failed");

            // Key rename
            await database.StringSetAsync("key:old", "value");
            var renamed = await database.KeyRenameAsync("key:old", "key:new");
            if (!renamed) throw new Exception("Key RENAME failed");

            exists = await database.KeyExistsAsync("key:new");
            if (!exists) throw new Exception("Key RENAME verification failed");
        }

        private static async Task TestConnectionAndServer(IDatabase database, IServer server)
        {
            // Ping
            var pingTime = await database.PingAsync();
            if (pingTime.TotalMilliseconds <= 0) throw new Exception("PING failed");

            // Database size
            var dbSize = await server.DatabaseSizeAsync();
            if (dbSize < 0) throw new Exception("DBSIZE failed");

            // Server info
            var info = await server.InfoAsync();
            if (info == null || info.Length == 0) throw new Exception("INFO command failed");

            // Get server time
            var serverTime = await server.TimeAsync();
            if (serverTime == DateTime.MinValue) throw new Exception("TIME command failed");
        }

        private static async Task TestTtlAndExpiration(IDatabase database)
        {
            // Set key with expiration
            await database.StringSetAsync("key:expire", "temporary", TimeSpan.FromSeconds(5));
            
            var ttl = await database.KeyTimeToLiveAsync("key:expire");
            if (!ttl.HasValue || ttl.Value.TotalSeconds <= 0 || ttl.Value.TotalSeconds > 5) 
                throw new Exception("Key expiration SET failed");

            // Set expiration on existing key
            await database.StringSetAsync("key:temp", "value");
            var expireSet = await database.KeyExpireAsync("key:temp", TimeSpan.FromSeconds(10));
            if (!expireSet) throw new Exception("Key EXPIRE failed");

            ttl = await database.KeyTimeToLiveAsync("key:temp");
            if (!ttl.HasValue || ttl.Value.TotalSeconds <= 0) throw new Exception("Key TTL failed");

            // Remove expiration
            var persistResult = await database.KeyPersistAsync("key:temp");
            if (!persistResult) throw new Exception("Key PERSIST failed");

            ttl = await database.KeyTimeToLiveAsync("key:temp");
            if (ttl.HasValue) throw new Exception("Key PERSIST verification failed");
        }

        private static async Task TestErrorConditions(IDatabase database)
        {
            // Test operations on wrong data types
            await database.StringSetAsync("error:string", "value");
            
            try
            {
                await database.HashGetAsync("error:string", "field");
                // If we get here without exception on a properly implemented server, that's actually ok
                // Some servers might return null/empty instead of throwing
            }
            catch (RedisServerException)
            {
                // Expected for strict Redis servers
            }

            // Test invalid key names (very long key)
            var longKey = new string('a', 10000);
            try
            {
                await database.StringSetAsync(longKey, "value");
                // Some implementations might accept this
            }
            catch (Exception)
            {
                // Expected for some implementations
            }

            // Test operations on non-existent keys
            var nonExistentValue = await database.StringGetAsync("error:nonexistent");
            if (nonExistentValue.HasValue) throw new Exception("Non-existent key should return null");

            var nonExistentHash = await database.HashGetAllAsync("error:nonexistent:hash");
            if (nonExistentHash.Length != 0) throw new Exception("Non-existent hash should return empty");
        }

        private static async Task TestPerformanceAndStress(IDatabase database)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Rapid SET operations
            var setTasks = new Task[100];
            for (int i = 0; i < 100; i++)
            {
                var key = $"perf:key{i}";
                var value = $"value{i}";
                setTasks[i] = database.StringSetAsync(key, value);
            }
            
            await Task.WhenAll(setTasks);
            stopwatch.Stop();
            
            if (stopwatch.ElapsedMilliseconds > 5000) // 5 second timeout for 100 operations
                throw new Exception($"SET performance too slow: {stopwatch.ElapsedMilliseconds}ms for 100 operations");

            // Rapid GET operations
            stopwatch.Restart();
            var getTasks = new Task<RedisValue>[100];
            for (int i = 0; i < 100; i++)
            {
                var key = $"perf:key{i}";
                getTasks[i] = database.StringGetAsync(key);
            }
            
            var results = await Task.WhenAll(getTasks);
            stopwatch.Stop();
            
            if (stopwatch.ElapsedMilliseconds > 5000)
                throw new Exception($"GET performance too slow: {stopwatch.ElapsedMilliseconds}ms for 100 operations");
            
            if (results.Any(r => !r.HasValue))
                throw new Exception("Some GET operations returned null unexpectedly");

            // Large value test
            var largeValue = new string('X', 1024 * 10); // 10KB
            await database.StringSetAsync("perf:large", largeValue);
            
            var retrievedLarge = await database.StringGetAsync("perf:large");
            if (retrievedLarge != largeValue) throw new Exception("Large value storage/retrieval failed");

            // Cleanup performance test keys
            var deleteKeys = new RedisKey[100];
            for (int i = 0; i < 100; i++)
            {
                deleteKeys[i] = $"perf:key{i}";
            }
            await database.KeyDeleteAsync(deleteKeys);
        }

        private static void DisplayTestSummary(List<TestResult> testResults, TimeSpan totalDuration)
        {
            Console.WriteLine("=" + new string('=', 59));
            Console.WriteLine("REDIS DATA TYPE TEST SUMMARY");
            Console.WriteLine("=" + new string('=', 59));

            var passedTests = testResults.Where(r => r.Passed).ToList();
            var failedTests = testResults.Where(r => !r.Passed).ToList();

            Console.WriteLine($"Total Test Categories: {testResults.Count}");
            Console.WriteLine($"[PASS] Passed: {passedTests.Count}");
            Console.WriteLine($"[FAIL] Failed: {failedTests.Count}");
            Console.WriteLine($"Total Runtime: {totalDuration.TotalSeconds:F1} seconds");
            Console.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            if (testResults.Count > 0)
            {
                Console.WriteLine($"Success Rate: {(passedTests.Count * 100.0 / testResults.Count):F1}%");
            }

            Console.WriteLine("\nDetailed Results:");
            Console.WriteLine("-" + new string('-', 58));

            foreach (var result in testResults)
            {
                var status = result.Passed ? "[PASS]" : "[FAIL]";
                var duration = result.Duration.TotalMilliseconds;
                Console.WriteLine($"{status} {result.TestName} - {duration:F0}ms");
                
                if (!result.Passed && result.Exception != null)
                {
                    Console.WriteLine($"   Error: {result.Exception.Message}");
                }
            }

            Console.WriteLine("\n" + "=" + new string('=', 59));
            
            if (failedTests.Count == 0)
            {
                Console.WriteLine("🎉 ALL REDIS DATA TYPE TESTS PASSED!");
                Console.WriteLine("The Redis server successfully handles all tested Redis data types and operations.");
            }
            else
            {
                Console.WriteLine($"❌ {failedTests.Count} TEST CATEGORY(IES) FAILED");
                Console.WriteLine("Please review the error details above and check server implementation.");
            }
            
            Console.WriteLine($">> Test suite completed in {totalDuration.TotalSeconds:F1} seconds");
        }

        private class TestResult
        {
            public string TestName { get; }
            public bool Passed { get; }
            public Exception? Exception { get; }
            public TimeSpan Duration { get; }

            public TestResult(string testName, bool passed, Exception? exception, TimeSpan duration)
            {
                TestName = testName;
                Passed = passed;
                Exception = exception;
                Duration = duration;
            }
        }
    }
}
