namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using Redish.Server.Models;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the <see cref="StringValue"/> model type and its numeric and text operations.
    /// </summary>
    public static class StringValueSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "StringValue";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the StringValue test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "default-empty", "Default constructor yields an empty string and String type", () =>
                {
                    StringValue v = new StringValue();
                    Check.Equal(string.Empty, v.Data);
                    Check.Equal(RedisValueType.String, v.Type);
                }),

                Cases.Sync(SuiteId, "null-becomes-empty", "Null data is normalized to an empty string", () =>
                {
                    StringValue v = new StringValue(null);
                    Check.Equal(string.Empty, v.Data);
                }),

                Cases.Sync(SuiteId, "stores-data", "Constructor stores provided data and ToString returns it", () =>
                {
                    StringValue v = new StringValue("hello world");
                    Check.Equal("hello world", v.Data);
                    Check.Equal("hello world", v.ToString());
                }),

                Cases.Sync(SuiteId, "increment", "Increment increases a numeric value by one", () =>
                {
                    StringValue v = new StringValue("10");
                    Check.Equal(11L, v.Increment());
                    Check.Equal("11", v.Data);
                }),

                Cases.Sync(SuiteId, "decrement", "Decrement decreases a numeric value by one", () =>
                {
                    StringValue v = new StringValue("10");
                    Check.Equal(9L, v.Decrement());
                    Check.Equal("9", v.Data);
                }),

                Cases.Sync(SuiteId, "increment-by", "IncrementBy adds the specified delta", () =>
                {
                    StringValue v = new StringValue("10");
                    Check.Equal(15L, v.IncrementBy(5));
                    Check.Equal(5L, v.IncrementBy(-10));
                }),

                Cases.Sync(SuiteId, "increment-negative-start", "Increment works from a negative starting value", () =>
                {
                    StringValue v = new StringValue("-3");
                    Check.Equal(-2L, v.Increment());
                }),

                Cases.Sync(SuiteId, "increment-non-numeric-throws", "Increment on a non-numeric value throws", () =>
                {
                    StringValue v = new StringValue("abc");
                    Check.Throws<InvalidOperationException>(() => v.Increment());
                }),

                Cases.Sync(SuiteId, "decrement-non-numeric-throws", "Decrement on a non-numeric value throws", () =>
                {
                    StringValue v = new StringValue("abc");
                    Check.Throws<InvalidOperationException>(() => v.Decrement());
                }),

                Cases.Sync(SuiteId, "append", "Append concatenates text and returns the new length", () =>
                {
                    StringValue v = new StringValue("foo");
                    int len = v.Append("bar");
                    Check.Equal(6, len);
                    Check.Equal("foobar", v.Data);
                }),

                Cases.Sync(SuiteId, "append-null", "Append of null is a no-op that returns the current length", () =>
                {
                    StringValue v = new StringValue("foo");
                    Check.Equal(3, v.Append(null));
                    Check.Equal("foo", v.Data);
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "StringValue model",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
