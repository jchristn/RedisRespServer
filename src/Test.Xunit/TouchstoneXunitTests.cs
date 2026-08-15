namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// Executes the shared Touchstone suites through xUnit, surfacing one xUnit test per
    /// Touchstone test case via the adapter's theory-data source.
    /// </summary>
    public sealed class TouchstoneXunitTests
    {

        #region Public-Members

        /// <summary>
        /// Gets the theory data: one row per non-skipped test case across all shared suites.
        /// </summary>
        /// <returns>The adapter-provided theory data source.</returns>
        public static TouchstoneTheoryData Cases()
        {
            return new TouchstoneTheoryData(TestSuites.All);
        }

        #endregion


        #region Public-Methods

        /// <summary>
        /// Runs a single Touchstone test case.
        /// </summary>
        /// <param name="testCase">The test case descriptor supplied by the theory data.</param>
        /// <returns>A task that completes when the case has executed.</returns>
        [Theory]
        [MemberData(nameof(Cases))]
        public async Task Run(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }

        #endregion

    }
}
