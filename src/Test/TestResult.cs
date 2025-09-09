namespace RedisResp.Tests
{
    using System;

    /// <summary>
    /// Represents the result of a single test execution.
    /// </summary>
    /// <remarks>
    /// This class encapsulates information about whether a test passed or failed,
    /// along with descriptive information and any error details that occurred during execution.
    /// It is used by the TestRunner to track and report on individual test outcomes.
    /// </remarks>
    public class TestResult
    {
        /// <summary>
        /// Gets or sets the name of the test that was executed.
        /// </summary>
        /// <value>A descriptive name identifying the specific test case.</value>
        public string TestName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the test passed.
        /// </summary>
        /// <value>true if the test completed successfully; false if it failed.</value>
        public bool Passed { get; set; }

        /// <summary>
        /// Gets or sets the error message if the test failed.
        /// </summary>
        /// <value>A detailed error message explaining why the test failed, or null if the test passed.</value>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the exception that caused the test to fail.
        /// </summary>
        /// <value>The exception that occurred during test execution, or null if no exception occurred.</value>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the test was executed.
        /// </summary>
        /// <value>The UTC timestamp when the test started execution.</value>
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the duration of the test execution.
        /// </summary>
        /// <value>The time span representing how long the test took to complete.</value>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestResult"/> class.
        /// </summary>
        public TestResult()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestResult"/> class with the specified test name.
        /// </summary>
        /// <param name="testName">The name of the test.</param>
        public TestResult(string testName)
        {
            TestName = testName;
        }

        /// <summary>
        /// Creates a successful test result.
        /// </summary>
        /// <param name="testName">The name of the test that passed.</param>
        /// <param name="duration">The duration of the test execution.</param>
        /// <returns>A TestResult instance indicating success.</returns>
        public static TestResult Success(string testName, TimeSpan duration)
        {
            return new TestResult(testName)
            {
                Passed = true,
                Duration = duration
            };
        }

        /// <summary>
        /// Creates a failed test result with an error message.
        /// </summary>
        /// <param name="testName">The name of the test that failed.</param>
        /// <param name="errorMessage">The error message describing the failure.</param>
        /// <param name="duration">The duration of the test execution.</param>
        /// <returns>A TestResult instance indicating failure.</returns>
        public static TestResult Failure(string testName, string errorMessage, TimeSpan duration)
        {
            return new TestResult(testName)
            {
                Passed = false,
                ErrorMessage = errorMessage,
                Duration = duration
            };
        }

        /// <summary>
        /// Creates a failed test result with an exception.
        /// </summary>
        /// <param name="testName">The name of the test that failed.</param>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <param name="duration">The duration of the test execution.</param>
        /// <returns>A TestResult instance indicating failure.</returns>
        public static TestResult Failure(string testName, Exception exception, TimeSpan duration)
        {
            return new TestResult(testName)
            {
                Passed = false,
                Exception = exception,
                ErrorMessage = exception?.Message,
                Duration = duration
            };
        }

        /// <summary>
        /// Returns a string representation of the test result.
        /// </summary>
        /// <returns>A formatted string showing the test name, result, and duration.</returns>
        public override string ToString()
        {
            var status = Passed ? "[PASS]" : "[FAIL]";
            var result = $"{status} - {TestName} ({Duration.TotalMilliseconds:F0}ms)";
            
            if (!Passed && !string.IsNullOrEmpty(ErrorMessage))
            {
                result += $" - {ErrorMessage}";
            }
            
            return result;
        }
    }
}