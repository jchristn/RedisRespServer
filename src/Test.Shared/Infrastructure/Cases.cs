namespace Test.Shared.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Convenience factory methods for constructing <see cref="TestCaseDescriptor"/> instances.
    /// </summary>
    /// <remarks>
    /// These helpers reduce repetition in the suite definitions and provide a single place to
    /// adapt synchronous test bodies to the asynchronous delegate shape Touchstone expects.
    /// </remarks>
    public static class Cases
    {

        #region Public-Members

        #endregion


        #region Private-Members

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Creates a test case from a synchronous body.
        /// </summary>
        /// <param name="suiteId">The parent suite identifier.</param>
        /// <param name="caseId">The unique case identifier within the suite.</param>
        /// <param name="displayName">The human-readable case name.</param>
        /// <param name="body">The synchronous test body.</param>
        /// <returns>A configured <see cref="TestCaseDescriptor"/>.</returns>
        public static TestCaseDescriptor Sync(string suiteId, string caseId, string displayName, Action body)
        {
            return new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: caseId,
                displayName: displayName,
                executeAsync: ct =>
                {
                    body();
                    return Task.CompletedTask;
                });
        }

        /// <summary>
        /// Creates a test case from an asynchronous body.
        /// </summary>
        /// <param name="suiteId">The parent suite identifier.</param>
        /// <param name="caseId">The unique case identifier within the suite.</param>
        /// <param name="displayName">The human-readable case name.</param>
        /// <param name="body">The asynchronous test body.</param>
        /// <returns>A configured <see cref="TestCaseDescriptor"/>.</returns>
        public static TestCaseDescriptor Async(
            string suiteId, string caseId, string displayName, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: caseId,
                displayName: displayName,
                executeAsync: body);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
