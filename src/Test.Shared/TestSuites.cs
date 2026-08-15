namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Suites;
    using Touchstone.Core;

    /// <summary>
    /// Central registry of every Touchstone test suite for the RedisRespServer library and the
    /// Redish.Server platform. This is the single source of truth exercised by every runner
    /// (the console CLI, the xUnit adapter, and the NUnit adapter).
    /// </summary>
    public static class TestSuites
    {

        #region Public-Members

        /// <summary>
        /// Gets all test suites in a stable, deterministic order.
        /// </summary>
        /// <value>The complete list of suites.</value>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    // Core RedisRespServer library - unit level
                    RedisValueSuite.Build(),
                    EnumSuite.Build(),
                    EventArgsSuite.Build(),
                    ClientInfoSuite.Build(),

                    // Core RedisRespServer library - listener and protocol (integration)
                    RespListenerLifecycleSuite.Build(),
                    Resp2ParsingSuite.Build(),
                    Resp3ParsingSuite.Build(),
                    RespNegativeSuite.Build(),
                    ClientConnectionSuite.Build(),
                    RespInterfaceSuite.Build(),
                    StressSuite.Build(),

                    // Redish.Server platform - model types (unit)
                    StringValueSuite.Build(),
                    HashValueSuite.Build(),
                    ListValueSuite.Build(),
                    SetValueSuite.Build(),
                    SortedSetValueSuite.Build(),
                    JsonValueSuite.Build(),
                    StreamValueSuite.Build(),

                    // Redish.Server platform - storage engine (unit)
                    StorageSuite.Build(),

                    // Redish.Server platform - end-to-end command dispatch (integration)
                    ServerCommandSuite.Build(),
                };
            }
        }

        #endregion


        #region Private-Members

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        #endregion


        #region Private-Methods

        #endregion

    }
}
