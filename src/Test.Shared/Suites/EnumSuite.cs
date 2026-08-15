namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the public enumerations exposed by the RedisRespServer library.
    /// </summary>
    public static class EnumSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "Enums";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the enumeration test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "resp-version-values", "RespVersionEnum has the correct numeric values", () =>
                {
                    Check.Equal(2, (int)RespVersionEnum.RESP2);
                    Check.Equal(3, (int)RespVersionEnum.RESP3);
                }),

                Cases.Sync(SuiteId, "severity-order", "SeverityEnum values are ordered by increasing severity", () =>
                {
                    Check.Equal(0, (int)SeverityEnum.Debug);
                    Check.Equal(1, (int)SeverityEnum.Info);
                    Check.Equal(2, (int)SeverityEnum.Warn);
                    Check.Equal(3, (int)SeverityEnum.Error);
                    Check.Equal(4, (int)SeverityEnum.Alert);
                    Check.Equal(5, (int)SeverityEnum.Critical);
                    Check.Equal(6, (int)SeverityEnum.Emergency);
                }),

                Cases.Sync(SuiteId, "resp-datatype-defined", "All RESP data types round-trip through Enum parsing", () =>
                {
                    foreach (string name in Enum.GetNames(typeof(RespDataType)))
                    {
                        RespDataType parsed = (RespDataType)Enum.Parse(typeof(RespDataType), name);
                        Check.Equal(name, parsed.ToString());
                    }
                }),

                Cases.Sync(SuiteId, "resp-datatype-count", "RespDataType exposes the full RESP2 and RESP3 type set", () =>
                {
                    // RESP2: SimpleString, Error, Integer, BulkString, Array, Null (6)
                    // RESP3 adds: Double, Boolean, BigNumber, BlobError, VerbatimString, Map, Set, Attribute, Push (9)
                    Check.Equal(15, Enum.GetValues(typeof(RespDataType)).Length);
                }),

                Cases.Sync(SuiteId, "redis-valuetype-defined", "RedisValueType exposes all supported value types", () =>
                {
                    Check.Equal(7, Enum.GetValues(typeof(RedisValueType)).Length);
                    Check.True(Enum.IsDefined(typeof(RedisValueType), RedisValueType.String));
                    Check.True(Enum.IsDefined(typeof(RedisValueType), RedisValueType.Hash));
                    Check.True(Enum.IsDefined(typeof(RedisValueType), RedisValueType.List));
                    Check.True(Enum.IsDefined(typeof(RedisValueType), RedisValueType.Set));
                    Check.True(Enum.IsDefined(typeof(RedisValueType), RedisValueType.SortedSet));
                    Check.True(Enum.IsDefined(typeof(RedisValueType), RedisValueType.Json));
                    Check.True(Enum.IsDefined(typeof(RedisValueType), RedisValueType.Stream));
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Public enumerations",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
