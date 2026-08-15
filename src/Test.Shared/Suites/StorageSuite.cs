namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Redish.Server.Models;
    using Redish.Server.Storage;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the <see cref="DictionaryStorage"/> engine and the shared <see cref="StorageBase"/> behavior.
    /// </summary>
    public static class StorageSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "Storage";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the storage engine test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "tryadd-new-and-duplicate", "TryAdd succeeds for a new key and fails for a duplicate", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        Check.True(s.TryAdd("k", new StringValue("v")));
                        Check.False(s.TryAdd("k", new StringValue("v2")));
                        Check.Equal(1, s.Count);
                    }
                }),

                Cases.Sync(SuiteId, "indexer-get-set", "The indexer stores and retrieves values", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["k"] = new StringValue("v");
                        RedisValue got = s["k"];
                        Check.NotNull(got);
                        Check.Equal("v", ((StringValue)got).Data);
                        Check.Null(s["missing"]);
                    }
                }),

                Cases.Sync(SuiteId, "indexer-set-null-throws", "Assigning a null value through the indexer throws", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        Check.Throws<ArgumentNullException>(() => s["k"] = null);
                    }
                }),

                Cases.Sync(SuiteId, "trygetvalue", "TryGetValue reports presence and yields the value", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["k"] = new StringValue("v");
                        Check.True(s.TryGetValue("k", out RedisValue v));
                        Check.Equal("v", ((StringValue)v).Data);
                        Check.False(s.TryGetValue("nope", out RedisValue _));
                    }
                }),

                Cases.Sync(SuiteId, "trygetvalue-typed", "The generic TryGetValue enforces the requested type", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["str"] = new StringValue("v");
                        Check.True(s.TryGetValue("str", out StringValue sv));
                        Check.Equal("v", sv.Data);
                        Check.False(s.TryGetValue("str", out HashValue _), "A string must not be returned as a hash");
                    }
                }),

                Cases.Sync(SuiteId, "tryremove", "TryRemove deletes a key and returns the removed value", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["k"] = new StringValue("v");
                        Check.True(s.TryRemove("k", out RedisValue removed));
                        Check.Equal("v", ((StringValue)removed).Data);
                        Check.False(s.TryRemove("k", out RedisValue _));
                        Check.Equal(0, s.Count);
                    }
                }),

                Cases.Sync(SuiteId, "tryupdate-reference", "TryUpdate replaces only when the comparison value matches", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        StringValue original = new StringValue("v1");
                        s["k"] = original;
                        StringValue replacement = new StringValue("v2");
                        Check.True(s.TryUpdate("k", replacement, original));
                        Check.Equal("v2", ((StringValue)s["k"]).Data);
                        Check.False(s.TryUpdate("k", new StringValue("v3"), new StringValue("not-current")));
                    }
                }),

                Cases.Sync(SuiteId, "get-all-keys", "GetAllKeys returns every stored key", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["a"] = new StringValue("1");
                        s["b"] = new StringValue("2");
                        List<string> keys = s.GetAllKeys().OrderBy(x => x).ToList();
                        Check.SequenceEqual(new[] { "a", "b" }, keys);
                    }
                }),

                Cases.Sync(SuiteId, "clear", "Clear empties the storage", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["a"] = new StringValue("1");
                        s["b"] = new StringValue("2");
                        s.Clear();
                        Check.Equal(0, s.Count);
                    }
                }),

                Cases.Sync(SuiteId, "exists", "Exists reports single and counts multiple keys", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["a"] = new StringValue("1");
                        s["b"] = new StringValue("2");
                        Check.True(s.Exists("a"));
                        Check.False(s.Exists("z"));
                        Check.Equal(2, s.Exists("a", "b", "z"));
                    }
                }),

                Cases.Sync(SuiteId, "get-type", "GetType returns the stored value's type or null", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["str"] = new StringValue("1");
                        s["hash"] = new HashValue();
                        Check.Equal(RedisValueType.String, s.GetType("str"));
                        Check.Equal(RedisValueType.Hash, s.GetType("hash"));
                        Check.Null(s.GetType("missing"));
                    }
                }),

                Cases.Sync(SuiteId, "del-multiple", "Del removes multiple keys and returns the deleted count", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["a"] = new StringValue("1");
                        s["b"] = new StringValue("2");
                        s["c"] = new StringValue("3");
                        Check.Equal(2, s.Del("a", "b", "missing"));
                        Check.Equal(1, s.Count);
                    }
                }),

                Cases.Sync(SuiteId, "expire-persist-ttl", "Expire sets a TTL and Persist removes it", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["k"] = new StringValue("v");
                        Check.Equal(-1, s.Ttl("k"), "no expiration yet");
                        Check.True(s.Expire("k", 60));
                        int ttl = s.Ttl("k");
                        Check.True(ttl > 0 && ttl <= 60, "ttl should be within (0,60], was " + ttl);
                        Check.True(s.Persist("k"));
                        Check.Equal(-1, s.Ttl("k"));
                    }
                }),

                Cases.Sync(SuiteId, "expire-missing", "Expire on a missing key returns false", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        Check.False(s.Expire("nope", 10));
                    }
                }),

                Cases.Sync(SuiteId, "ttl-missing", "Ttl on a missing key returns -2", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        Check.Equal(-2, s.Ttl("nope"));
                    }
                }),

                Cases.Sync(SuiteId, "expired-eviction-on-access", "An expired value is evicted and reported absent on access", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        StringValue v = new StringValue("v");
                        v.ExpiresAt = DateTime.UtcNow.AddSeconds(-5);
                        s["k"] = v;
                        Check.False(s.TryGetValue("k", out RedisValue _));
                        Check.False(s.Exists("k"));
                        Check.Equal(-2, s.Ttl("k"));
                    }
                }),

                Cases.Sync(SuiteId, "rename", "Rename moves a value to a new key", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["old"] = new StringValue("v");
                        Check.True(s.Rename("old", "new"));
                        Check.False(s.Exists("old"));
                        Check.Equal("v", ((StringValue)s["new"]).Data);
                    }
                }),

                Cases.Sync(SuiteId, "rename-missing", "Rename of a missing key returns false", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        Check.False(s.Rename("missing", "new"));
                    }
                }),

                Cases.Sync(SuiteId, "renamenx", "RenameNx only renames when the target does not exist", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["old"] = new StringValue("v");
                        s["taken"] = new StringValue("other");
                        Check.False(s.RenameNx("old", "taken"));
                        Check.True(s.RenameNx("old", "fresh"));
                        Check.Equal("v", ((StringValue)s["fresh"]).Data);
                    }
                }),

                Cases.Sync(SuiteId, "random-key-empty", "RandomKey returns null when storage is empty", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        Check.Null(s.RandomKey());
                    }
                }),

                Cases.Sync(SuiteId, "random-key", "RandomKey returns an existing key", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["a"] = new StringValue("1");
                        Check.Equal("a", s.RandomKey());
                    }
                }),

                Cases.Sync(SuiteId, "pattern-star", "GetKeysByPattern matches a trailing wildcard", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["user:1"] = new StringValue("a");
                        s["user:2"] = new StringValue("b");
                        s["other"] = new StringValue("c");
                        List<string> matched = s.GetKeysByPattern("user:*").OrderBy(x => x).ToList();
                        Check.SequenceEqual(new[] { "user:1", "user:2" }, matched);
                    }
                }),

                Cases.Sync(SuiteId, "pattern-question", "GetKeysByPattern matches a single-character wildcard", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s["user:1"] = new StringValue("a");
                        s["user:12"] = new StringValue("b");
                        List<string> matched = s.GetKeysByPattern("user:?").ToList();
                        Check.SequenceEqual(new[] { "user:1" }, matched);
                    }
                }),

                Cases.Sync(SuiteId, "get-or-add", "GetOrAdd returns existing or creates via factory", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        RedisValue created = s.GetOrAdd("k", _ => new StringValue("new"));
                        Check.Equal("new", ((StringValue)created).Data);
                        RedisValue existing = s.GetOrAdd("k", _ => new StringValue("other"));
                        Check.Equal("new", ((StringValue)existing).Data);
                    }
                }),

                Cases.Sync(SuiteId, "add-or-update", "AddOrUpdate adds when absent and updates when present", () =>
                {
                    using (DictionaryStorage s = new DictionaryStorage())
                    {
                        s.AddOrUpdate("k", new StringValue("init"), (key, existing) => new StringValue("updated"));
                        Check.Equal("init", ((StringValue)s["k"]).Data);
                        s.AddOrUpdate("k", new StringValue("init"), (key, existing) => new StringValue("updated"));
                        Check.Equal("updated", ((StringValue)s["k"]).Data);
                    }
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Storage engine",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
