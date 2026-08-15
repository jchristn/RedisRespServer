namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using Redish.Server.Models;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the <see cref="ListValue"/> model type and its push, pop, and range operations.
    /// </summary>
    public static class ListValueSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "ListValue";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the ListValue test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "type-empty", "A new list reports the List type and zero length", () =>
                {
                    ListValue l = new ListValue();
                    Check.Equal(RedisValueType.List, l.Type);
                    Check.Equal(0, l.LLen());
                }),

                Cases.Sync(SuiteId, "rpush-appends", "RPush appends to the tail in order and returns the new length", () =>
                {
                    ListValue l = new ListValue();
                    Check.Equal(3, l.RPush("a", "b", "c"));
                    Check.SequenceEqual(new[] { "a", "b", "c" }, l.LRange(0, -1));
                }),

                Cases.Sync(SuiteId, "lpush-prepends-reversed", "LPush inserts at the head so the arg order is preserved at the front", () =>
                {
                    ListValue l = new ListValue();
                    l.RPush("c");
                    Check.Equal(3, l.LPush("a", "b"));
                    // LPush(a,b) inserts reversed at front: [b, a] then existing [c] => b, a, c
                    Check.SequenceEqual(new[] { "b", "a", "c" }, l.LRange(0, -1));
                }),

                Cases.Sync(SuiteId, "rpop", "RPop removes and returns the tail element", () =>
                {
                    ListValue l = new ListValue();
                    l.RPush("a", "b", "c");
                    Check.Equal("c", l.RPop());
                    Check.Equal(2, l.LLen());
                }),

                Cases.Sync(SuiteId, "lpop", "LPop removes and returns the head element", () =>
                {
                    ListValue l = new ListValue();
                    l.RPush("a", "b", "c");
                    Check.Equal("a", l.LPop());
                    Check.Equal(2, l.LLen());
                }),

                Cases.Sync(SuiteId, "pop-empty-null", "Popping an empty list returns null", () =>
                {
                    ListValue l = new ListValue();
                    Check.Null(l.RPop());
                    Check.Null(l.LPop());
                }),

                Cases.Sync(SuiteId, "lrange-negative", "LRange supports negative indices relative to the tail", () =>
                {
                    ListValue l = new ListValue();
                    l.RPush("a", "b", "c", "d", "e");
                    Check.SequenceEqual(new[] { "d", "e" }, l.LRange(-2, -1));
                    Check.SequenceEqual(new[] { "a", "b", "c", "d", "e" }, l.LRange(0, -1));
                }),

                Cases.Sync(SuiteId, "lrange-clamped", "LRange clamps out-of-bounds indices", () =>
                {
                    ListValue l = new ListValue();
                    l.RPush("a", "b", "c");
                    Check.SequenceEqual(new[] { "a", "b", "c" }, l.LRange(0, 100));
                }),

                Cases.Sync(SuiteId, "lrange-inverted-empty", "LRange returns empty when start is past stop", () =>
                {
                    ListValue l = new ListValue();
                    l.RPush("a", "b", "c");
                    Check.Equal(0, l.LRange(2, 1).Length);
                }),

                Cases.Sync(SuiteId, "lrange-empty-list", "LRange on an empty list returns an empty array", () =>
                {
                    Check.Equal(0, new ListValue().LRange(0, -1).Length);
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "ListValue model",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
