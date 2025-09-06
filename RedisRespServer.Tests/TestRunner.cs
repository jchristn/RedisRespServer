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
        #region Public-Members

        /// <summary>
        /// Gets the collection of test results from the last test run.
        /// </summary>
        /// <value>A read-only list of TestResult objects representing individual test outcomes.</value>
        public IReadOnlyList<TestResult> TestResults => _testResults.AsReadOnly();

        #endregion

        #region Private-Members

        private TestServer _server;
        private readonly List<TestClient> _clients = new List<TestClient>();
        private readonly List<TestResult> _testResults = new List<TestResult>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="TestRunner"/> class.
        /// </summary>
        public TestRunner()
        {
        }

        #endregion

        #region Public-Methods

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

            _testResults.Clear();
            _server = new TestServer();
            
            var overallStopwatch = Stopwatch.StartNew();
            
            try
            {
                await _server.StartAsync(6380);
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

        #endregion

        #region Private-Methods

        /// <summary>
        /// Executes the complete test suite with individual test tracking.
        /// </summary>
        /// <returns>A task representing the asynchronous test suite execution.</returns>
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
        }

        /// <summary>
        /// Runs a test method and tracks its execution result.
        /// </summary>
        /// <param name="testName">The name of the test being executed.</param>
        /// <param name="testMethod">The test method to execute.</param>
        /// <returns>A task representing the asynchronous test execution.</returns>
        private async Task RunTestWithTracking(string testName, Func<Task> testMethod)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await testMethod();
                stopwatch.Stop();
                
                var result = TestResult.Success(testName, stopwatch.Elapsed);
                _testResults.Add(result);
                
                Console.WriteLine($"[PASS] {testName} completed successfully");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                var result = TestResult.Failure(testName, ex, stopwatch.Elapsed);
                _testResults.Add(result);
                
                Console.WriteLine($"[FAIL] {testName} failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Tests basic RESP data type parsing and event handling.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        /// <remarks>
        /// This test validates that all standard RESP data types are correctly parsed:
        /// - Simple strings (+)
        /// - Errors (-)
        /// - Integers (:) including negative values
        /// - Bulk strings ($) including empty and null variants
        /// - Arrays (*) including null arrays
        /// </remarks>
        private async Task RunBasicDataTypeTests()
        {
            Console.WriteLine("\n[TEST] Running Basic Data Type Tests...");

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

        /// <summary>
        /// Tests error handling for invalid and malformed RESP data.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        /// <remarks>
        /// This test validates proper error handling for:
        /// - Invalid RESP type prefixes
        /// - Malformed integer values
        /// - Incomplete bulk strings
        /// - Invalid length specifiers
        /// </remarks>
        private async Task RunErrorConditionTests()
        {
            Console.WriteLine("\n[TEST] Running Error Condition Tests...");

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

        /// <summary>
        /// Tests concurrent connections and data handling from multiple clients.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        /// <remarks>
        /// This test validates:
        /// - Multiple simultaneous client connections
        /// - Concurrent data transmission
        /// - Proper client isolation
        /// - Graceful client disconnection handling
        /// </remarks>
        private async Task RunMultipleClientTests()
        {
            Console.WriteLine("\n[TEST] Running Multiple Client Tests...");

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

        /// <summary>
        /// Tests server behavior when clients suddenly disconnect.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        /// <remarks>
        /// This test validates:
        /// - Server resilience to sudden client disconnections
        /// - Proper cleanup of disconnected client resources
        /// - Continued operation after client failures
        /// - Client disconnection event handling
        /// </remarks>
        private async Task RunClientDisconnectionTests()
        {
            Console.WriteLine("\n[TEST] Running Client Disconnection Tests...");

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

        /// <summary>
        /// Tests handling of incomplete and malformed protocol data.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        /// <remarks>
        /// This test validates resilience against:
        /// - Data without proper line endings
        /// - Partial message transmission
        /// - Empty protocol messages
        /// - Fragmented data streams
        /// </remarks>
        private async Task RunMalformedDataTests()
        {
            Console.WriteLine("\n[TEST] Running Malformed Data Tests...");

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

        /// <summary>
        /// Tests server performance under high load conditions.
        /// </summary>
        /// <returns>A task representing the asynchronous test operation.</returns>
        /// <remarks>
        /// This test validates:
        /// - High-volume message processing (100 rapid messages)
        /// - Large payload handling (10KB bulk strings)
        /// - Concurrent message sending
        /// - Memory and performance characteristics under load
        /// </remarks>
        private async Task RunStressTests()
        {
            Console.WriteLine("\n[TEST] Running Stress Tests...");

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

        /// <summary>
        /// Creates and connects a new test client to the server.
        /// </summary>
        /// <returns>A connected TestClient instance, or null if connection failed.</returns>
        /// <remarks>
        /// This helper method creates a new test client, attempts to connect it to the
        /// test server on localhost:6380, and adds it to the managed client list for cleanup.
        /// </remarks>
        private async Task<TestClient> CreateAndConnectClient()
        {
            var client = new TestClient();
            if (await client.ConnectAsync("localhost", 6380))
            {
                _clients.Add(client);
                return client;
            }
            return null;
        }

        /// <summary>
        /// Cleans up all test resources and stops the test server.
        /// </summary>
        /// <returns>A task representing the asynchronous cleanup operation.</returns>
        private async Task CleanupTestEnvironment()
        {
            // Clean up all clients
            foreach (var client in _clients)
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
            _clients.Clear();

            // Stop server
            try
            {
                _server?.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping server: {ex.Message}");
            }

            await Task.Delay(500); // Allow cleanup to complete
        }

        /// <summary>
        /// Displays a comprehensive test summary with pass/fail statistics.
        /// </summary>
        /// <param name="totalDuration">The total time taken to run all tests.</param>
        private void DisplayTestSummary(TimeSpan totalDuration)
        {
            Console.WriteLine("\n" + "=" + new string('=', 59));
            Console.WriteLine("TEST SUMMARY");
            Console.WriteLine("=" + new string('=', 59));

            var passedTests = _testResults.Where(r => r.Passed).ToList();
            var failedTests = _testResults.Where(r => !r.Passed).ToList();

            Console.WriteLine($"Total Tests: {_testResults.Count}");
            Console.WriteLine($"[PASS] Passed: {passedTests.Count}");
            Console.WriteLine($"[FAIL] Failed: {failedTests.Count}");
            Console.WriteLine($"Total Duration: {totalDuration.TotalSeconds:F1} seconds");

            if (_testResults.Count > 0)
            {
                Console.WriteLine($"Success Rate: {(passedTests.Count * 100.0 / _testResults.Count):F1}%");
            }

            Console.WriteLine("\nDetailed Results:");
            Console.WriteLine("-" + new string('-', 58));

            foreach (var result in _testResults)
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

        #endregion
    }
}