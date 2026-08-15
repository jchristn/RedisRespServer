namespace Test.Automated
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Cli;

    /// <summary>
    /// Console entry point that executes every shared Touchstone suite through the built-in
    /// tabular console runner. Returns a non-zero exit code when any test fails, making it
    /// suitable for CI pipelines.
    /// </summary>
    internal static class Program
    {

        #region Public-Methods

        /// <summary>
        /// Runs all shared test suites.
        /// </summary>
        /// <param name="args">Optional single argument: a path to which JSON results are written.</param>
        /// <returns>Zero when all tests pass; one when any test fails.</returns>
        private static async Task<int> Main(string[] args)
        {
            string resultsPath = args != null && args.Length > 0 ? args[0] : null;
            return await ConsoleRunner.RunAsync(TestSuites.All, null, resultsPath, CancellationToken.None)
                .ConfigureAwait(false);
        }

        #endregion

    }
}
