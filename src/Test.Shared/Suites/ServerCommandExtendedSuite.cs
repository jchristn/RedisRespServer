namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// End-to-end tests that drive a real in-process <see cref="Redish.Server.RedishServer"/> over TCP,
    /// covering the extended command surface beyond the basic key commands: numeric operations, the string
    /// range/length family, hashes, lists, key expiration, TYPE, and DBSIZE. Both positive results and
    /// negative conditions (wrong argument counts, non-numeric input, and cross-type WRONGTYPE errors) are
    /// verified against the exact RESP wire responses the server produces.
    /// </summary>
    /// <remarks>
    /// Every case boots its own isolated server instance so the suite behaves identically whether run
    /// through the console runner or through the per-case xUnit and NUnit adapters. Raw clients connect
    /// without a HELLO handshake, so the server answers using RESP2 framing throughout.
    /// </remarks>
    public static class ServerCommandExtendedSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "ServerCommandsExtended";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the extended end-to-end server command test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                // ---- Numeric string operations ----

                Cases.Async(SuiteId, "incr-new", "INCR on a missing key initializes it to 1", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal(":1\r\n", await ServerFixture.SendCommandAsync(c, "INCR", "counter"));
                    });
                }),

                Cases.Async(SuiteId, "incr-existing", "INCR increments an existing integer value", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "n", "10");
                        Check.Equal(":11\r\n", await ServerFixture.SendCommandAsync(c, "INCR", "n"));
                    });
                }),

                Cases.Async(SuiteId, "decr-new", "DECR on a missing key initializes it to -1", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal(":-1\r\n", await ServerFixture.SendCommandAsync(c, "DECR", "d"));
                    });
                }),

                Cases.Async(SuiteId, "incrby", "INCRBY accumulates across calls", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal(":5\r\n", await ServerFixture.SendCommandAsync(c, "INCRBY", "x", "5"));
                        Check.Equal(":8\r\n", await ServerFixture.SendCommandAsync(c, "INCRBY", "x", "3"));
                    });
                }),

                Cases.Async(SuiteId, "incr-non-integer", "INCR on a non-numeric value returns an error", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "s", "abc");
                        string resp = await ServerFixture.SendCommandAsync(c, "INCR", "s");
                        Check.True(resp.StartsWith("-ERR"), "expected an error, got [" + Readable(resp) + "]");
                        Check.StringContains("not an integer", resp);
                    });
                }),

                Cases.Async(SuiteId, "incrby-bad-increment", "INCRBY with a non-numeric increment returns an error", async ct =>
                {
                    await WithClient(async c =>
                    {
                        string resp = await ServerFixture.SendCommandAsync(c, "INCRBY", "y", "notanumber");
                        Check.StringContains("not an integer", resp);
                    });
                }),

                Cases.Async(SuiteId, "incr-wrongtype", "INCR against a list key returns WRONGTYPE", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "RPUSH", "mylist", "a");
                        string resp = await ServerFixture.SendCommandAsync(c, "INCR", "mylist");
                        Check.StringContains("WRONGTYPE", resp);
                    });
                }),

                // ---- String range / length / multi ----

                Cases.Async(SuiteId, "strlen", "STRLEN returns the value length and 0 for missing keys", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "k", "hello");
                        Check.Equal(":5\r\n", await ServerFixture.SendCommandAsync(c, "STRLEN", "k"));
                        Check.Equal(":0\r\n", await ServerFixture.SendCommandAsync(c, "STRLEN", "missing"));
                    });
                }),

                Cases.Async(SuiteId, "getrange", "GETRANGE returns the requested substring", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "g", "Hello World");
                        Check.Equal("$5\r\nHello\r\n", await ServerFixture.SendCommandAsync(c, "GETRANGE", "g", "0", "4"));
                    });
                }),

                Cases.Async(SuiteId, "mset-mget", "MSET stores pairs and MGET returns values with nulls for gaps", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal("+OK\r\n", await ServerFixture.SendCommandAsync(c, "MSET", "k1", "v1", "k2", "v2"));
                        Check.Equal(
                            "*3\r\n$2\r\nv1\r\n$-1\r\n$2\r\nv2\r\n",
                            await ServerFixture.SendCommandAsync(c, "MGET", "k1", "nope", "k2"));
                    });
                }),

                Cases.Async(SuiteId, "mset-odd-args", "MSET with an unpaired argument returns an error", async ct =>
                {
                    await WithClient(async c =>
                    {
                        string resp = await ServerFixture.SendCommandAsync(c, "MSET", "a", "b", "c");
                        Check.True(resp.StartsWith("-ERR"), "expected an error, got [" + Readable(resp) + "]");
                    });
                }),

                // ---- Hash operations ----

                Cases.Async(SuiteId, "hset-hget", "HSET reports new fields and HGET reads them back", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal(":1\r\n", await ServerFixture.SendCommandAsync(c, "HSET", "h", "f1", "v1"));
                        Check.Equal("$2\r\nv1\r\n", await ServerFixture.SendCommandAsync(c, "HGET", "h", "f1"));
                    });
                }),

                Cases.Async(SuiteId, "hget-missing-field", "HGET on an absent field returns a null bulk string", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "HSET", "h", "f1", "v1");
                        Check.Equal("$-1\r\n", await ServerFixture.SendCommandAsync(c, "HGET", "h", "nope"));
                    });
                }),

                Cases.Async(SuiteId, "hgetall", "HGETALL returns a flat field/value array", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "HSET", "h2", "only", "1");
                        Check.Equal(
                            "*2\r\n$4\r\nonly\r\n$1\r\n1\r\n",
                            await ServerFixture.SendCommandAsync(c, "HGETALL", "h2"));
                    });
                }),

                Cases.Async(SuiteId, "hlen-hexists", "HLEN counts fields and HEXISTS reports membership", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "HSET", "h", "f1", "v1");
                        Check.Equal(":1\r\n", await ServerFixture.SendCommandAsync(c, "HLEN", "h"));
                        Check.Equal(":1\r\n", await ServerFixture.SendCommandAsync(c, "HEXISTS", "h", "f1"));
                        Check.Equal(":0\r\n", await ServerFixture.SendCommandAsync(c, "HEXISTS", "h", "nope"));
                    });
                }),

                Cases.Async(SuiteId, "hdel", "HDEL removes a field and HEXISTS then reports it absent", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "HSET", "h", "f1", "v1");
                        Check.Equal(":1\r\n", await ServerFixture.SendCommandAsync(c, "HDEL", "h", "f1"));
                        Check.Equal(":0\r\n", await ServerFixture.SendCommandAsync(c, "HEXISTS", "h", "f1"));
                    });
                }),

                Cases.Async(SuiteId, "hget-wrongtype", "HGET against a string key returns WRONGTYPE", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "str", "v");
                        string resp = await ServerFixture.SendCommandAsync(c, "HGET", "str", "f");
                        Check.StringContains("WRONGTYPE", resp);
                    });
                }),

                Cases.Async(SuiteId, "hset-wrong-args", "HSET without a value returns an error", async ct =>
                {
                    await WithClient(async c =>
                    {
                        string resp = await ServerFixture.SendCommandAsync(c, "HSET", "h", "f1");
                        Check.True(resp.StartsWith("-ERR"), "expected an error, got [" + Readable(resp) + "]");
                    });
                }),

                // ---- List operations ----

                Cases.Async(SuiteId, "rpush-lrange", "RPUSH appends in order and LRANGE returns the whole list", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal(":3\r\n", await ServerFixture.SendCommandAsync(c, "RPUSH", "L", "a", "b", "c"));
                        Check.Equal(
                            "*3\r\n$1\r\na\r\n$1\r\nb\r\n$1\r\nc\r\n",
                            await ServerFixture.SendCommandAsync(c, "LRANGE", "L", "0", "-1"));
                    });
                }),

                Cases.Async(SuiteId, "lpush-llen", "LPUSH prepends and LLEN counts the elements", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "LPUSH", "L2", "x");
                        await ServerFixture.SendCommandAsync(c, "LPUSH", "L2", "y");
                        Check.Equal(":2\r\n", await ServerFixture.SendCommandAsync(c, "LLEN", "L2"));
                        Check.Equal(
                            "*2\r\n$1\r\ny\r\n$1\r\nx\r\n",
                            await ServerFixture.SendCommandAsync(c, "LRANGE", "L2", "0", "-1"));
                    });
                }),

                Cases.Async(SuiteId, "lpop-rpop", "LPOP takes from the head and RPOP from the tail", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "RPUSH", "L3", "a", "b", "c");
                        Check.Equal("$1\r\na\r\n", await ServerFixture.SendCommandAsync(c, "LPOP", "L3"));
                        Check.Equal("$1\r\nc\r\n", await ServerFixture.SendCommandAsync(c, "RPOP", "L3"));
                    });
                }),

                Cases.Async(SuiteId, "lpop-missing", "LPOP on a missing key returns a null bulk string", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal("$-1\r\n", await ServerFixture.SendCommandAsync(c, "LPOP", "nope"));
                    });
                }),

                Cases.Async(SuiteId, "llen-wrongtype", "LLEN against a string key returns WRONGTYPE", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "str", "v");
                        string resp = await ServerFixture.SendCommandAsync(c, "LLEN", "str");
                        Check.StringContains("WRONGTYPE", resp);
                    });
                }),

                // ---- Expiration ----

                Cases.Async(SuiteId, "ttl-missing", "TTL on a missing key returns -2", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal(":-2\r\n", await ServerFixture.SendCommandAsync(c, "TTL", "nope"));
                    });
                }),

                Cases.Async(SuiteId, "ttl-no-expiry", "TTL on a key without an expiration returns -1", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "k", "v");
                        Check.Equal(":-1\r\n", await ServerFixture.SendCommandAsync(c, "TTL", "k"));
                    });
                }),

                Cases.Async(SuiteId, "expire-and-ttl", "EXPIRE sets a TTL that TTL then reports as positive", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "k", "v");
                        Check.Equal(":1\r\n", await ServerFixture.SendCommandAsync(c, "EXPIRE", "k", "100"));
                        string resp = await ServerFixture.SendCommandAsync(c, "TTL", "k");
                        Check.True(resp.StartsWith(":"), "expected an integer reply, got [" + Readable(resp) + "]");
                        int ttl = int.Parse(resp.Substring(1).Trim(), CultureInfo.InvariantCulture);
                        Check.True(ttl > 0 && ttl <= 100, "TTL should be within (0,100], was " + ttl);
                    });
                }),

                Cases.Async(SuiteId, "expire-missing", "EXPIRE on a missing key returns 0", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal(":0\r\n", await ServerFixture.SendCommandAsync(c, "EXPIRE", "nope", "100"));
                    });
                }),

                Cases.Async(SuiteId, "expire-invalid-seconds", "EXPIRE with a non-positive time returns an error", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "k", "v");
                        string resp = await ServerFixture.SendCommandAsync(c, "EXPIRE", "k", "0");
                        Check.StringContains("invalid expire time", resp);
                    });
                }),

                Cases.Async(SuiteId, "expire-non-integer", "EXPIRE with a non-numeric time returns an error", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "k", "v");
                        string resp = await ServerFixture.SendCommandAsync(c, "EXPIRE", "k", "soon");
                        Check.StringContains("not an integer", resp);
                    });
                }),

                Cases.Async(SuiteId, "persist", "PERSIST removes an expiration once and is idempotent thereafter", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "k", "v");
                        await ServerFixture.SendCommandAsync(c, "EXPIRE", "k", "100");
                        Check.Equal(":1\r\n", await ServerFixture.SendCommandAsync(c, "PERSIST", "k"));
                        Check.Equal(":0\r\n", await ServerFixture.SendCommandAsync(c, "PERSIST", "k"));
                        Check.Equal(":-1\r\n", await ServerFixture.SendCommandAsync(c, "TTL", "k"));
                    });
                }),

                // ---- TYPE and DBSIZE ----

                Cases.Async(SuiteId, "type-string", "TYPE reports 'string' for a string key", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "k", "v");
                        Check.Equal("+string\r\n", await ServerFixture.SendCommandAsync(c, "TYPE", "k"));
                    });
                }),

                Cases.Async(SuiteId, "type-none", "TYPE reports 'none' for a missing key", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal("+none\r\n", await ServerFixture.SendCommandAsync(c, "TYPE", "nope"));
                    });
                }),

                Cases.Async(SuiteId, "type-hash", "TYPE reports 'hash' for a hash key", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "HSET", "h", "f", "v");
                        Check.Equal("+hash\r\n", await ServerFixture.SendCommandAsync(c, "TYPE", "h"));
                    });
                }),

                Cases.Async(SuiteId, "type-list", "TYPE reports 'list' for a list key", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "RPUSH", "L", "a");
                        Check.Equal("+list\r\n", await ServerFixture.SendCommandAsync(c, "TYPE", "L"));
                    });
                }),

                Cases.Async(SuiteId, "dbsize", "DBSIZE reflects the number of stored keys", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "a", "1");
                        await ServerFixture.SendCommandAsync(c, "SET", "b", "2");
                        Check.Equal(":2\r\n", await ServerFixture.SendCommandAsync(c, "DBSIZE"));
                    });
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Server end-to-end commands (extended)",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        /// <summary>
        /// Boots an isolated in-process server, connects a single client, runs the body, then tears down.
        /// </summary>
        /// <param name="body">The test body that receives the connected client.</param>
        /// <returns>A task that completes when the body and teardown finish.</returns>
        private static async Task WithClient(Func<RespRawClient, Task> body)
        {
            using (ServerFixture fixture = await ServerFixture.StartAsync())
            using (RespRawClient client = await fixture.ConnectAsync())
            {
                await body(client);
            }
        }

        /// <summary>
        /// Produces a readable form of a response for failure messages.
        /// </summary>
        /// <param name="value">The response.</param>
        /// <returns>A CRLF-escaped representation.</returns>
        private static string Readable(string value)
        {
            return value == null ? "null" : value.Replace("\r", "\\r").Replace("\n", "\\n");
        }

        #endregion

    }
}
