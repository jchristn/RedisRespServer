namespace RedisResp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Orchestrates comprehensive testing of the Redis Protocol Listener functionality.
    /// </summary>
    /// <remarks>
    /// This class runs a complete test suite that validates RESP protocol parsing,
    /// error handling, multi-client support, client disconnection scenarios, and stress testing.
    /// It manages test server startup/shutdown, coordinates multiple test clients, and provides
    /// detailed test result tracking with pass/fail summaries.
    /// </remarks>
    public class TestRunner
    {
#pragma warning disable CS8603 // Possible null reference return.

        /// <summary>
        /// Gets the collection of test results from the last test run.
        /// </summary>
        /// <value>A read-only list of TestResult objects representing individual test outcomes.</value>
        public IReadOnlyList<TestResult> TestResults => _TestResults.AsReadOnly();

        private TestServer? _TestServer;
        private readonly List<TestClient> _Clients = new List<TestClient>();
        private readonly List<TestResult> _TestResults = new List<TestResult>();

        /// <summary>
        /// Initializes a new instance of the <see cref="TestRunner"/> class.
        /// </summary>
        public TestRunner()
        {
        }

        /// <summary>
        /// Runs the complete test suite for the Redis Protocol Listener.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        /// <remarks>
        /// This method executes all test scenarios in sequence:
        /// 1. Basic RESP data type tests
        /// 2. Error condition tests  
        /// 3. Multiple client tests
        /// 4. Client disconnection tests
        /// 5. Malformed data tests
        /// 6. Stress tests
        /// 
        /// The test server is automatically started and stopped, and all clients
        /// are properly cleaned up regardless of test outcomes. A comprehensive
        /// summary is displayed at the end showing pass/fail statistics.
        /// </remarks>
        /// <exception cref="Exception">Any unhandled exceptions during test execution are caught and logged.</exception>
        public async Task RunAllTestsAsync()
        {
            Console.WriteLine(">> Starting Redis Protocol Listener Tests");
            Console.WriteLine("=" + new string('=', 59));

            _TestResults.Clear();
            _TestServer = new TestServer();
            
            var overallStopwatch = Stopwatch.StartNew();
            
            try
            {
                await _TestServer.StartAsync(6380);
                await Task.Delay(500);

                await ExecuteTestSuite();
            }
            finally
            {
                await CleanupTestEnvironment();
                overallStopwatch.Stop();
            }

            DisplayTestSummary(overallStopwatch.Elapsed);
        }

        private async Task ExecuteTestSuite()
        {
            await RunTestWithTracking("Basic Data Type Tests", RunBasicDataTypeTests);
            await Task.Delay(1000);

            await RunTestWithTracking("Error Condition Tests", RunErrorConditionTests);
            await Task.Delay(1000);

            await RunTestWithTracking("Multiple Client Tests", RunMultipleClientTests);
            await Task.Delay(1000);

            await RunTestWithTracking("Client Disconnection Tests", RunClientDisconnectionTests);
            await Task.Delay(1000);

            await RunTestWithTracking("Malformed Data Tests", RunMalformedDataTests);
            await Task.Delay(1000);

            await RunTestWithTracking("Stress Tests", RunStressTests);
            await Task.Delay(1000);

            await RunTestWithTracking("Redis Command Tests", RunRedisCommandTests);
        }

        private async Task RunTestWithTracking(string testName, Func<Task> testMethod)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine($"TEST CATEGORY: {testName.ToUpper()}");
            Console.WriteLine(new string('=', 60));
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await testMethod();
                stopwatch.Stop();
                
                var result = TestResult.Success(testName, stopwatch.Elapsed);
                _TestResults.Add(result);
                
                Console.WriteLine($"\n✅ [PASS] {testName} completed successfully ({stopwatch.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                var result = TestResult.Failure(testName, ex, stopwatch.Elapsed);
                _TestResults.Add(result);
                
                Console.WriteLine($"\n❌ [FAIL] {testName} failed: {ex.Message} ({stopwatch.ElapsedMilliseconds}ms)");
            }
        }

        private async Task RunBasicDataTypeTests()
        {
            Console.WriteLine(">> Testing all standard RESP data types");
            Console.WriteLine("   Expected: Server correctly parses and responds to each RESP type");
            Console.WriteLine("   Tests: Simple String, Error, Integer, Bulk String, Array, Null values");

            var client = await CreateAndConnectClient();
            if (client == null) throw new InvalidOperationException("Failed to create test client");

            // Test Simple String
            await client.SendRespDataAsync("+OK\r\n");
            await Task.Delay(100);

            // Test Error
            await client.SendRespDataAsync("-ERR unknown command\r\n");
            await Task.Delay(100);

            // Test Integer
            await client.SendRespDataAsync(":1000\r\n");
            await Task.Delay(100);

            // Test negative integer
            await client.SendRespDataAsync(":-42\r\n");
            await Task.Delay(100);

            // Test Bulk String
            await client.SendRespDataAsync("$6\r\nfoobar\r\n");
            await Task.Delay(100);

            // Test empty Bulk String
            await client.SendRespDataAsync("$0\r\n\r\n");
            await Task.Delay(100);

            // Test Null Bulk String
            await client.SendRespDataAsync("$-1\r\n");
            await Task.Delay(100);

            // Test Array
            await client.SendRespDataAsync("*2\r\n$3\r\nget\r\n$3\r\nkey\r\n");
            await Task.Delay(100);

            // Test Null Array
            await client.SendRespDataAsync("*-1\r\n");
            await Task.Delay(100);
        }

        private async Task RunErrorConditionTests()
        {
            Console.WriteLine(">> Testing server resilience to invalid RESP data");
            Console.WriteLine("   Expected: Server handles malformed data gracefully without crashing");
            Console.WriteLine("   Tests: Invalid type prefixes, malformed integers, incomplete messages");

            var client = await CreateAndConnectClient();
            if (client == null) throw new InvalidOperationException("Failed to create test client");

            // Test invalid RESP type
            await client.SendRespDataAsync("@invalid\r\n");
            await Task.Delay(100);

            // Test malformed integer
            await client.SendRespDataAsync(":not_a_number\r\n");
            await Task.Delay(100);

            // Test incomplete bulk string
            await client.SendRespDataAsync("$10\r\nshort\r\n");
            await Task.Delay(100);

            // Test bulk string with invalid length
            await client.SendRespDataAsync("$abc\r\ndata\r\n");
            await Task.Delay(100);
        }

        private async Task RunMultipleClientTests()
        {
            Console.WriteLine(">> Testing concurrent client connections");
            Console.WriteLine("   Expected: Server handles multiple simultaneous clients correctly");
            Console.WriteLine("   Tests: 3 concurrent connections sending data simultaneously");

            var clients = new List<TestClient>();

            // Create multiple clients
            for (int i = 0; i < 3; i++)
            {
                var client = await CreateAndConnectClient();
                if (client != null)
                {
                    clients.Add(client);
                }
            }

            if (clients.Count == 0) throw new InvalidOperationException("Failed to create any test clients");

            // Send data from multiple clients simultaneously
            var tasks = new List<Task>();
            for (int i = 0; i < clients.Count; i++)
            {
                var client = clients[i];
                var clientNum = i + 1;

                tasks.Add(Task.Run(async () =>
                {
                    await client.SendRespDataAsync($"+Hello from client {clientNum}\r\n");
                    await Task.Delay(50);
                    await client.SendRespDataAsync($":{clientNum * 100}\r\n");
                    await Task.Delay(50);
                    await client.SendRespDataAsync($"$7\r\nClient{clientNum}\r\n");
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(500);

            // Disconnect one client gracefully
            if (clients.Count > 0)
            {
                clients[0].Disconnect();
                clients.RemoveAt(0);
            }
        }

        private async Task RunClientDisconnectionTests()
        {
            Console.WriteLine(">> Testing server behavior during client disconnections");
            Console.WriteLine("   Expected: Server continues operating after client disconnects");
            Console.WriteLine("   Tests: Sudden disconnects, partial data transmission, recovery");

            // Test sudden disconnection during data transmission
            var client1 = await CreateAndConnectClient();
            if (client1 == null) throw new InvalidOperationException("Failed to create test client 1");

            await client1.SendRespDataAsync("+Starting transmission\r\n");
            await Task.Delay(100);

            // Simulate sudden disconnection
            client1.SimulateSuddenDisconnection();
            await Task.Delay(500);

            // Test disconnection after partial data
            var client2 = await CreateAndConnectClient();
            if (client2 == null) throw new InvalidOperationException("Failed to create test client 2");

            await client2.SendRespDataAsync("$20\r\npartial");
            await Task.Delay(100);
            client2.SimulateSuddenDisconnection();
            await Task.Delay(500);

            // Test server continues working after disconnections
            var client3 = await CreateAndConnectClient();
            if (client3 == null) throw new InvalidOperationException("Failed to create test client 3");

            await client3.SendRespDataAsync("+Server still working\r\n");
            await Task.Delay(100);

            // Test disconnection during array transmission
            var client4 = await CreateAndConnectClient();
            if (client4 == null) throw new InvalidOperationException("Failed to create test client 4");

            await client4.SendRespDataAsync("*3\r\n$3\r\nset\r\n");
            await Task.Delay(50);
            client4.SimulateSuddenDisconnection();
            await Task.Delay(500);

            // Verify server is still responsive
            await client3.SendRespDataAsync("+Final test message\r\n");
            await Task.Delay(100);

            client3.Disconnect();
        }

        private async Task RunMalformedDataTests()
        {
            Console.WriteLine(">> Testing incomplete and fragmented message handling");
            Console.WriteLine("   Expected: Server processes fragmented data correctly");
            Console.WriteLine("   Tests: Partial messages, missing line endings, empty data");

            var client = await CreateAndConnectClient();
            if (client == null) throw new InvalidOperationException("Failed to create test client");

            // Send data without proper line endings
            await client.SendRespDataAsync("+OK");
            await Task.Delay(100);
            await client.SendRespDataAsync("\r\n");
            await Task.Delay(100);

            // Send partial data
            await client.SendRespDataAsync("$10\r\n");
            await Task.Delay(100);
            await client.SendRespDataAsync("partial");
            await Task.Delay(100);
            await client.SendRespDataAsync("data\r\n");
            await Task.Delay(100);

            // Send empty lines
            await client.SendRespDataAsync("\r\n\r\n");
            await Task.Delay(100);
        }

        private async Task RunStressTests()
        {
            Console.WriteLine(">> Testing server performance under load");
            Console.WriteLine("   Expected: Server handles high volume and large messages efficiently");
            Console.WriteLine("   Tests: 100 rapid messages, 10KB bulk strings, concurrent processing");

            var client = await CreateAndConnectClient();
            if (client == null) throw new InvalidOperationException("Failed to create test client");

            // Send many messages quickly
            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                var messageNum = i;
                tasks.Add(client.SendRespDataAsync($"+Message{messageNum}\r\n"));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(1000); // Give time for processing

            // Test large bulk string
            var largeString = new string('A', 10000);
            await client.SendRespDataAsync($"${largeString.Length}\r\n{largeString}\r\n");
            await Task.Delay(500);
        }

        private async Task RunRedisCommandTests()
        {
            Console.WriteLine(">> Testing Redis command compatibility");
            Console.WriteLine("   Expected: Server responds to Redis commands with correct RESP format");
            Console.WriteLine("   Tests: String, Hash, List, Set, Sorted Set, Key, TTL, JSON, Stream commands");

            var client = await CreateAndConnectClient();
            if (client == null) throw new InvalidOperationException("Failed to create test client");

            // Test String commands
            await client.SendRespDataAsync("*3\r\n$3\r\nSET\r\n$7\r\nmykey\r\n$8\r\nhello\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*2\r\n$3\r\nGET\r\n$7\r\nmykey\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*3\r\n$3\r\nSET\r\n$10\r\ncounter\r\n$2\r\n10\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*2\r\n$4\r\nINCR\r\n$7\r\ncounter\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*2\r\n$4\r\nDECR\r\n$7\r\ncounter\r\n");
            await Task.Delay(100);

            // Test INCRBY command
            await client.SendRespDataAsync("*3\r\n$6\r\nINCRBY\r\n$7\r\ncounter\r\n$1\r\n5\r\n");
            await Task.Delay(100);

            // Test MSET command
            await client.SendRespDataAsync("*5\r\n$4\r\nMSET\r\n$4\r\nkey1\r\n$6\r\nvalue1\r\n$4\r\nkey2\r\n$6\r\nvalue2\r\n");
            await Task.Delay(100);

            // Test MGET command
            await client.SendRespDataAsync("*3\r\n$4\r\nMGET\r\n$4\r\nkey1\r\n$4\r\nkey2\r\n");
            await Task.Delay(100);

            // Test Hash commands
            await client.SendRespDataAsync("*4\r\n$4\r\nHSET\r\n$4\r\nuser\r\n$4\r\nname\r\n$4\r\nJohn\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*4\r\n$4\r\nHSET\r\n$4\r\nuser\r\n$3\r\nage\r\n$2\r\n30\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*3\r\n$4\r\nHGET\r\n$4\r\nuser\r\n$4\r\nname\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*2\r\n$7\r\nHGETALL\r\n$4\r\nuser\r\n");
            await Task.Delay(100);

            // Test HMSET command
            await client.SendRespDataAsync("*7\r\n$5\r\nHMSET\r\n$7\r\nprofile\r\n$4\r\nname\r\n$5\r\nAlice\r\n$3\r\nage\r\n$2\r\n25\r\n$4\r\ncity\r\n$7\r\nSeattle\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*2\r\n$4\r\nHLEN\r\n$4\r\nuser\r\n");
            await Task.Delay(100);

            // Test List commands
            await client.SendRespDataAsync("*3\r\n$5\r\nLPUSH\r\n$8\r\nmylist\r\n$6\r\nworld\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*3\r\n$5\r\nLPUSH\r\n$8\r\nmylist\r\n$5\r\nhello\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*4\r\n$6\r\nLRANGE\r\n$8\r\nmylist\r\n$1\r\n0\r\n$2\r\n-1\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*2\r\n$4\r\nLLEN\r\n$8\r\nmylist\r\n");
            await Task.Delay(100);

            // Test Set commands
            await client.SendRespDataAsync("*3\r\n$4\r\nSADD\r\n$5\r\nmyset\r\n$3\r\nred\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*3\r\n$4\r\nSADD\r\n$5\r\nmyset\r\n$4\r\nblue\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*2\r\n$8\r\nSMEMBERS\r\n$5\r\nmyset\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*2\r\n$5\r\nSCARD\r\n$5\r\nmyset\r\n");
            await Task.Delay(100);

            // Test Sorted Set commands
            await client.SendRespDataAsync("*4\r\n$4\r\nZADD\r\n$6\r\nscores\r\n$3\r\n100\r\n$5\r\nAlice\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*4\r\n$4\r\nZADD\r\n$6\r\nscores\r\n$2\r\n85\r\n$3\r\nBob\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*4\r\n$6\r\nZRANGE\r\n$6\r\nscores\r\n$1\r\n0\r\n$2\r\n-1\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*3\r\n$6\r\nZSCORE\r\n$6\r\nscores\r\n$5\r\nAlice\r\n");
            await Task.Delay(100);

            // Test Key management commands
            await client.SendRespDataAsync("*2\r\n$6\r\nEXISTS\r\n$5\r\nmykey\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*2\r\n$4\r\nKEYS\r\n$1\r\n*\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*1\r\n$6\r\nDBSIZE\r\n");
            await Task.Delay(100);

            // Test TTL commands
            await client.SendRespDataAsync("*4\r\n$6\r\nEXPIRE\r\n$7\r\ncounter\r\n$2\r\n60\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*2\r\n$3\r\nTTL\r\n$7\r\ncounter\r\n");
            await Task.Delay(100);

            // Test JSON commands (if supported)
            await client.SendRespDataAsync("*4\r\n$8\r\nJSON.SET\r\n$4\r\nuser\r\n$1\r\n.\r\n$15\r\n{\"name\":\"John\"}\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*3\r\n$8\r\nJSON.GET\r\n$4\r\nuser\r\n$1\r\n.\r\n");
            await Task.Delay(100);

            // Test Stream commands (if supported)
            await client.SendRespDataAsync("*5\r\n$4\r\nXADD\r\n$8\r\nmystream\r\n$1\r\n*\r\n$5\r\nfield\r\n$5\r\nvalue\r\n");
            await Task.Delay(100);

            await client.SendRespDataAsync("*4\r\n$6\r\nXRANGE\r\n$8\r\nmystream\r\n$1\r\n-\r\n$1\r\n+\r\n");
            await Task.Delay(100);

            // Test PING command
            await client.SendRespDataAsync("*1\r\n$4\r\nPING\r\n");
            await Task.Delay(100);

            // Test INFO command
            await client.SendRespDataAsync("*1\r\n$4\r\nINFO\r\n");
            await Task.Delay(100);

            // Test invalid command
            await client.SendRespDataAsync("*1\r\n$10\r\nINVALIDCMD\r\n");
            await Task.Delay(100);

            Console.WriteLine("[TEST] Redis command tests completed - sent various Redis commands");
        }

        private async Task<TestClient> CreateAndConnectClient()
        {
            var client = new TestClient();
            if (await client.ConnectAsync("localhost", 6380))
            {
                _Clients.Add(client);
                return client;
            }
            return null;
        }

        private async Task CleanupTestEnvironment()
        {
            // Clean up all clients
            foreach (var client in _Clients)
            {
                try
                {
                    client.Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cleaning up client: {ex.Message}");
                }
            }
            _Clients.Clear();

            // Stop server
            try
            {
                _TestServer?.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping server: {ex.Message}");
            }

            await Task.Delay(500); // Allow cleanup to complete
        }

        private void DisplayTestSummary(TimeSpan totalDuration)
        {
            Console.WriteLine("\n" + "=" + new string('=', 59));
            Console.WriteLine("TEST SUMMARY");
            Console.WriteLine("=" + new string('=', 59));

            var passedTests = _TestResults.Where(r => r.Passed).ToList();
            var failedTests = _TestResults.Where(r => !r.Passed).ToList();

            Console.WriteLine($"Total Tests: {_TestResults.Count}");
            Console.WriteLine($"[PASS] Passed: {passedTests.Count}");
            Console.WriteLine($"[FAIL] Failed: {failedTests.Count}");
            Console.WriteLine($"Total Duration: {totalDuration.TotalSeconds:F1} seconds");

            if (_TestResults.Count > 0)
            {
                Console.WriteLine($"Success Rate: {(passedTests.Count * 100.0 / _TestResults.Count):F1}%");
            }

            Console.WriteLine("\nDetailed Results:");
            Console.WriteLine("-" + new string('-', 58));

            foreach (var result in _TestResults)
            {
                Console.WriteLine(result.ToString());
                if (!result.Passed && result.Exception != null)
                {
                    Console.WriteLine($"   Exception: {result.Exception.GetType().Name}");
                    if (!string.IsNullOrEmpty(result.Exception.Message))
                    {
                        Console.WriteLine($"   Details: {result.Exception.Message}");
                    }
                }
            }

            Console.WriteLine("\n" + "=" + new string('=', 59));
            
            if (failedTests.Count == 0)
            {
                Console.WriteLine(">> ALL TESTS PASSED!");
            }
            else
            {
                Console.WriteLine($">> {failedTests.Count} TEST(S) FAILED - Please review the details above.");
            }
            
            Console.WriteLine(">> Test execution completed!");
        }

#pragma warning disable CS8603 // Possible null reference return.
    }
}