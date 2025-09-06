namespace RedisResp.Tests
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// The main entry point for the Redis Protocol Listener test suite.
    /// </summary>
    /// <remarks>
    /// This class provides the console application entry point and orchestrates
    /// the execution of all Redis Protocol Listener tests. It handles top-level
    /// exception catching and provides user interaction for test execution.
    /// </remarks>
    public class TestProgram
    {
        #region Public-Methods

        /// <summary>
        /// The main entry point for the test application.
        /// </summary>
        /// <param name="args">Command line arguments (currently unused).</param>
        /// <returns>A task representing the asynchronous program execution.</returns>
        /// <remarks>
        /// This method initializes and runs the complete test suite for the Redis Protocol Listener.
        /// It provides comprehensive error handling and user feedback throughout the test execution.
        /// The application will wait for user input before exiting to allow review of test results.
        /// </remarks>
        /// <example>
        /// Run the test program from the command line:
        /// <code>
        /// dotnet run
        /// </code>
        /// </example>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Redis Protocol Listener Test Suite");
            Console.WriteLine("==================================");

            try
            {
                var testRunner = new TestRunner();
                await testRunner.RunAllTestsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Test execution failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        #endregion
    }
}