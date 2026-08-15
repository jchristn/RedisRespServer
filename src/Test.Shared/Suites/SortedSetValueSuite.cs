namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using Redish.Server.Models;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the <see cref="SortedSetValue"/> model type and its score-ordered operations.
    /// </summary>
    public static class SortedSetValueSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "SortedSetValue";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the SortedSetValue test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "type", "SortedSetValue reports the SortedSet type", () =>
                {
                    Check.Equal(RedisValueType.SortedSet, new SortedSetValue().Type);
                }),

                Cases.Sync(SuiteId, "zadd-counts-new", "ZAdd returns the number of newly added members", () =>
                {
                    SortedSetValue z = new SortedSetValue();
                    Check.Equal(2, z.ZAdd(100, "Alice", 85, "Bob"));
                    Check.Equal(2, z.ZCard());
                }),

                Cases.Sync(SuiteId, "zadd-update-not-counted", "ZAdd updates existing scores without counting them", () =>
                {
                    SortedSetValue z = new SortedSetValue();
                    z.ZAdd(100, "Alice");
                    Check.Equal(0, z.ZAdd(120, "Alice"));
                    Check.Equal(120.0, z.ZScore("Alice"));
                }),

                Cases.Sync(SuiteId, "zadd-odd-throws", "ZAdd with an odd number of arguments throws", () =>
                {
                    SortedSetValue z = new SortedSetValue();
                    Check.Throws<ArgumentException>(() => z.ZAdd(100, "Alice", 85));
                }),

                Cases.Sync(SuiteId, "zscore-missing-null", "ZScore returns null for an absent member", () =>
                {
                    Check.Null(new SortedSetValue().ZScore("nobody"));
                }),

                Cases.Sync(SuiteId, "zrem", "ZRem removes members and counts removals", () =>
                {
                    SortedSetValue z = new SortedSetValue();
                    z.ZAdd(1, "a", 2, "b", 3, "c");
                    Check.Equal(2, z.ZRem("a", "b", "missing"));
                    Check.Equal(1, z.ZCard());
                }),

                Cases.Sync(SuiteId, "zrange-ordered", "ZRange returns members ordered by ascending score", () =>
                {
                    SortedSetValue z = new SortedSetValue();
                    z.ZAdd(100, "Alice", 85, "Bob", 92, "Charlie");
                    Check.SequenceEqual(new[] { "Bob", "Charlie", "Alice" }, z.ZRange(0, -1));
                }),

                Cases.Sync(SuiteId, "zrange-withscores", "ZRange with scores interleaves member and score", () =>
                {
                    SortedSetValue z = new SortedSetValue();
                    z.ZAdd(85, "Bob", 100, "Alice");
                    string[] r = z.ZRange(0, -1, true);
                    Check.SequenceEqual(new[] { "Bob", "85", "Alice", "100" }, r);
                }),

                Cases.Sync(SuiteId, "zrange-negative", "ZRange supports negative indices", () =>
                {
                    SortedSetValue z = new SortedSetValue();
                    z.ZAdd(1, "a", 2, "b", 3, "c");
                    Check.SequenceEqual(new[] { "c" }, z.ZRange(-1, -1));
                }),

                Cases.Sync(SuiteId, "zincrby-existing", "ZIncrBy adjusts an existing score", () =>
                {
                    SortedSetValue z = new SortedSetValue();
                    z.ZAdd(85, "Bob");
                    Check.Equal(95.0, z.ZIncrBy(10, "Bob"));
                }),

                Cases.Sync(SuiteId, "zincrby-new", "ZIncrBy creates a member from a zero baseline", () =>
                {
                    SortedSetValue z = new SortedSetValue();
                    Check.Equal(5.0, z.ZIncrBy(5, "New"));
                    Check.Equal(5.0, z.ZScore("New"));
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "SortedSetValue model",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
