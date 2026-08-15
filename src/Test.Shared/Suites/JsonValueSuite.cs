namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using Redish.Server.Models;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the <see cref="JsonValue"/> model type and its JSON.SET/GET/DEL semantics.
    /// </summary>
    public static class JsonValueSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "JsonValue";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the JsonValue test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "default", "A new JsonValue defaults to an empty object and Json type", () =>
                {
                    JsonValue j = new JsonValue();
                    Check.Equal("{}", j.Data);
                    Check.Equal(RedisValueType.Json, j.Type);
                }),

                Cases.Sync(SuiteId, "null-defaults-empty-object", "Null constructor data defaults to an empty object", () =>
                {
                    Check.Equal("{}", new JsonValue(null).Data);
                }),

                Cases.Sync(SuiteId, "set-get-root", "JsonSet at root stores the document, JsonGet returns it", () =>
                {
                    JsonValue j = new JsonValue();
                    Check.True(j.JsonSet(".", "{\"name\":\"John\"}"));
                    Check.Equal("{\"name\":\"John\"}", j.JsonGet("."));
                }),

                Cases.Sync(SuiteId, "set-nx-on-empty-succeeds", "JsonSet with NX succeeds when the document is the empty default", () =>
                {
                    JsonValue j = new JsonValue();
                    Check.True(j.JsonSet(".", "{\"a\":1}", "NX"));
                }),

                Cases.Sync(SuiteId, "set-nx-on-existing-fails", "JsonSet with NX fails when a real document already exists", () =>
                {
                    JsonValue j = new JsonValue();
                    j.JsonSet(".", "{\"a\":1}");
                    Check.False(j.JsonSet(".", "{\"a\":2}", "NX"));
                }),

                Cases.Sync(SuiteId, "set-xx-on-empty-fails", "JsonSet with XX fails when no real document exists yet", () =>
                {
                    JsonValue j = new JsonValue();
                    Check.False(j.JsonSet(".", "{\"a\":1}", "XX"));
                }),

                Cases.Sync(SuiteId, "set-xx-on-existing-succeeds", "JsonSet with XX succeeds when a real document exists", () =>
                {
                    JsonValue j = new JsonValue();
                    j.JsonSet(".", "{\"a\":1}");
                    Check.True(j.JsonSet(".", "{\"a\":2}", "XX"));
                    Check.Equal("{\"a\":2}", j.JsonGet("."));
                }),

                Cases.Sync(SuiteId, "del-resets", "JsonDel resets the document and returns 1", () =>
                {
                    JsonValue j = new JsonValue();
                    j.JsonSet(".", "{\"a\":1}");
                    Check.Equal(1, j.JsonDel("."));
                    Check.Equal("{}", j.Data);
                }),

                Cases.Sync(SuiteId, "get-null-path-throws", "JsonGet with a null path throws", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new JsonValue().JsonGet(null));
                }),

                Cases.Sync(SuiteId, "set-null-path-throws", "JsonSet with a null path throws", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new JsonValue().JsonSet(null, "{}"));
                }),

                Cases.Sync(SuiteId, "set-null-value-throws", "JsonSet with a null value throws", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new JsonValue().JsonSet(".", null));
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "JsonValue model",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
