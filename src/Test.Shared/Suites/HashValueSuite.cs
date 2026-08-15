namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using Redish.Server.Models;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the <see cref="HashValue"/> model type and its field operations.
    /// </summary>
    public static class HashValueSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "HashValue";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the HashValue test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "type", "HashValue reports the Hash type", () =>
                {
                    Check.Equal(RedisValueType.Hash, new HashValue().Type);
                }),

                Cases.Sync(SuiteId, "set-and-get", "SetField stores a value retrievable via GetField", () =>
                {
                    HashValue h = new HashValue();
                    Check.True(h.SetField("name", "John"));
                    Check.Equal("John", h.GetField("name"));
                }),

                Cases.Sync(SuiteId, "get-missing-null", "GetField returns null for a missing field", () =>
                {
                    HashValue h = new HashValue();
                    Check.Null(h.GetField("missing"));
                }),

                Cases.Sync(SuiteId, "update-existing", "SetField updates the value of an existing field", () =>
                {
                    HashValue h = new HashValue();
                    h.SetField("age", "30");
                    h.SetField("age", "31");
                    Check.Equal("31", h.GetField("age"));
                    Check.Equal(1, h.FieldCount);
                }),

                Cases.Sync(SuiteId, "field-count", "FieldCount reflects the number of distinct fields", () =>
                {
                    HashValue h = new HashValue();
                    h.SetField("a", "1");
                    h.SetField("b", "2");
                    h.SetField("c", "3");
                    Check.Equal(3, h.FieldCount);
                }),

                Cases.Sync(SuiteId, "remove", "RemoveField removes a field and returns true, then false", () =>
                {
                    HashValue h = new HashValue();
                    h.SetField("x", "1");
                    Check.True(h.RemoveField("x"));
                    Check.False(h.RemoveField("x"));
                    Check.Equal(0, h.FieldCount);
                }),

                Cases.Sync(SuiteId, "get-all-flat", "GetAll returns a flat field/value array", () =>
                {
                    HashValue h = new HashValue();
                    h.SetField("k1", "v1");
                    h.SetField("k2", "v2");
                    string[] all = h.GetAll();
                    Check.Equal(4, all.Length);
                    // Order is not guaranteed; verify pairing by scanning.
                    Dictionary<string, string> reconstructed = new Dictionary<string, string>();
                    for (int i = 0; i < all.Length; i += 2) reconstructed[all[i]] = all[i + 1];
                    Check.Equal("v1", reconstructed["k1"]);
                    Check.Equal("v2", reconstructed["k2"]);
                }),

                Cases.Sync(SuiteId, "get-all-empty", "GetAll on an empty hash returns an empty array", () =>
                {
                    Check.Equal(0, new HashValue().GetAll().Length);
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "HashValue model",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
