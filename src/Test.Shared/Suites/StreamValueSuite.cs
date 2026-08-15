namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using Redish.Server.Models;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the <see cref="StreamValue"/> model type and its entry operations.
    /// </summary>
    public static class StreamValueSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "StreamValue";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the StreamValue test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "type-empty", "A new stream reports the Stream type and zero length", () =>
                {
                    StreamValue s = new StreamValue();
                    Check.Equal(RedisValueType.Stream, s.Type);
                    Check.Equal(0, s.XLen());
                }),

                Cases.Sync(SuiteId, "xadd-explicit-id", "XAdd with an explicit id stores the entry and its fields", () =>
                {
                    StreamValue s = new StreamValue();
                    string id = s.XAdd("1-0", "field", "value");
                    Check.Equal("1-0", id);
                    Check.Equal(1, s.XLen());
                    StreamEntry[] entries = s.XRange("-", "+");
                    Check.Equal(1, entries.Length);
                    Check.Equal("value", entries[0].Fields["field"]);
                }),

                Cases.Sync(SuiteId, "xadd-auto-id", "XAdd with '*' generates a timestamp-sequence id", () =>
                {
                    StreamValue s = new StreamValue();
                    string id = s.XAdd("*", "a", "1");
                    Check.NotNull(id);
                    Check.StringContains("-", id);
                    Check.Equal(1, s.XLen());
                }),

                Cases.Sync(SuiteId, "xadd-odd-fields-throws", "XAdd with an odd number of field arguments throws", () =>
                {
                    StreamValue s = new StreamValue();
                    Check.Throws<ArgumentException>(() => s.XAdd("1-0", "onlyfield"));
                }),

                Cases.Sync(SuiteId, "xadd-null-id-throws", "XAdd with a null id throws", () =>
                {
                    StreamValue s = new StreamValue();
                    Check.Throws<ArgumentNullException>(() => s.XAdd(null, "a", "1"));
                }),

                Cases.Sync(SuiteId, "xrange-all", "XRange with '-' and '+' returns every entry", () =>
                {
                    StreamValue s = new StreamValue();
                    s.XAdd("1-0", "a", "1");
                    s.XAdd("2-0", "b", "2");
                    s.XAdd("3-0", "c", "3");
                    Check.Equal(3, s.XRange("-", "+").Length);
                }),

                Cases.Sync(SuiteId, "xrange-count", "XRange honors the count limit", () =>
                {
                    StreamValue s = new StreamValue();
                    s.XAdd("1-0", "a", "1");
                    s.XAdd("2-0", "b", "2");
                    s.XAdd("3-0", "c", "3");
                    Check.Equal(2, s.XRange("-", "+", 2).Length);
                }),

                Cases.Sync(SuiteId, "xrange-bounded", "XRange filters entries within the id bounds", () =>
                {
                    StreamValue s = new StreamValue();
                    s.XAdd("1-0", "a", "1");
                    s.XAdd("2-0", "b", "2");
                    s.XAdd("3-0", "c", "3");
                    StreamEntry[] r = s.XRange("2-0", "2-0");
                    Check.Equal(1, r.Length);
                    Check.Equal("2-0", r[0].Id);
                }),

                Cases.Sync(SuiteId, "xdel", "XDel removes entries by id and counts removals", () =>
                {
                    StreamValue s = new StreamValue();
                    s.XAdd("1-0", "a", "1");
                    s.XAdd("2-0", "b", "2");
                    Check.Equal(1, s.XDel("1-0", "missing-0"));
                    Check.Equal(1, s.XLen());
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "StreamValue model",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
