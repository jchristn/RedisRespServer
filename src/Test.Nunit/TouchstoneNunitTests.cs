namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using global::NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Executes the shared Touchstone suites through NUnit, surfacing one NUnit test case per
    /// Touchstone test case via the adapter's test-case source.
    /// </summary>
    [TestFixture]
    public sealed class TouchstoneNunitTests
    {

        #region Public-Members

        #endregion


        #region Private-Members

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Provides one NUnit test-case entry per non-skipped Touchstone test case.
        /// </summary>
        /// <returns>An enumerable of test case descriptors.</returns>
        public static IEnumerable Cases()
        {
            return new TouchstoneTestCaseSource(TestSuites.All);
        }

        /// <summary>
        /// Runs a single Touchstone test case.
        /// </summary>
        /// <param name="testCase">The test case descriptor supplied by the source.</param>
        /// <returns>A task that completes when the case has executed.</returns>
        [TestCaseSource(nameof(Cases))]
        public async Task Run(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
