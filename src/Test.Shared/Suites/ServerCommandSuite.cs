namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// End-to-end tests that drive a real in-process <see cref="Redish.Server.RedishServer"/> over TCP
    /// using RESP command frames, verifying the full parse-dispatch-respond pipeline for core commands.
    /// </summary>
    /// <remarks>
    /// Every case boots its own isolated server instance so the suite behaves identically whether run
    /// through the console runner (which honors suite lifecycle hooks) or through the per-case xUnit and
    /// NUnit adapters (which execute each descriptor independently).
    /// </remarks>
    public static class ServerCommandSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "ServerCommands";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the end-to-end server command test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Async(SuiteId, "ping", "PING returns +PONG", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal("+PONG\r\n", await ServerFixture.SendCommandAsync(c, "PING"));
                    });
                }),

                Cases.Async(SuiteId, "set-returns-ok", "SET returns +OK", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal("+OK\r\n", await ServerFixture.SendCommandAsync(c, "SET", "k1", "hello"));
                    });
                }),

                Cases.Async(SuiteId, "get-existing", "GET returns the stored bulk string", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "k2", "hello");
                        Check.Equal("$5\r\nhello\r\n", await ServerFixture.SendCommandAsync(c, "GET", "k2"));
                    });
                }),

                Cases.Async(SuiteId, "get-missing-null", "GET on a missing key returns a null bulk string", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal("$-1\r\n", await ServerFixture.SendCommandAsync(c, "GET", "does-not-exist"));
                    });
                }),

                Cases.Async(SuiteId, "set-get-roundtrip-value", "A SET value round-trips through GET", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "rt", "world");
                        string resp = await ServerFixture.SendCommandAsync(c, "GET", "rt");
                        Check.StringContains("world", resp);
                    });
                }),

                Cases.Async(SuiteId, "del", "DEL returns 1 for an existing key and 0 for a missing one", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "d", "v");
                        Check.Equal(":1\r\n", await ServerFixture.SendCommandAsync(c, "DEL", "d"));
                        Check.Equal(":0\r\n", await ServerFixture.SendCommandAsync(c, "DEL", "d"));
                    });
                }),

                Cases.Async(SuiteId, "exists", "EXISTS returns 1 when present and 0 when absent", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "e", "v");
                        Check.Equal(":1\r\n", await ServerFixture.SendCommandAsync(c, "EXISTS", "e"));
                        Check.Equal(":0\r\n", await ServerFixture.SendCommandAsync(c, "EXISTS", "missing"));
                    });
                }),

                Cases.Async(SuiteId, "echo", "ECHO returns the payload as a bulk string", async ct =>
                {
                    await WithClient(async c =>
                    {
                        Check.Equal("$5\r\nhello\r\n", await ServerFixture.SendCommandAsync(c, "ECHO", "hello"));
                    });
                }),

                Cases.Async(SuiteId, "keys", "KEYS returns all matching keys as an array", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "a", "1");
                        await ServerFixture.SendCommandAsync(c, "SET", "b", "2");
                        string resp = await ServerFixture.SendCommandAsync(c, "KEYS", "*");
                        Check.True(resp.StartsWith("*2\r\n"), "expected a 2-element array, got [" + Readable(resp) + "]");
                        Check.StringContains("a", resp);
                        Check.StringContains("b", resp);
                    });
                }),

                Cases.Async(SuiteId, "flushdb", "FLUSHDB clears the keyspace", async ct =>
                {
                    await WithClient(async c =>
                    {
                        await ServerFixture.SendCommandAsync(c, "SET", "x", "1");
                        Check.Equal("+OK\r\n", await ServerFixture.SendCommandAsync(c, "FLUSHDB"));
                        Check.Equal("*0\r\n", await ServerFixture.SendCommandAsync(c, "KEYS", "*"));
                    });
                }),

                Cases.Async(SuiteId, "set-wrong-args", "SET with too few arguments returns an error", async ct =>
                {
                    await WithClient(async c =>
                    {
                        string resp = await ServerFixture.SendCommandAsync(c, "SET", "only-key");
                        Check.True(resp.StartsWith("-ERR"), "expected an error, got [" + Readable(resp) + "]");
                    });
                }),

                Cases.Async(SuiteId, "unknown-command", "An unknown command returns an unknown-command error", async ct =>
                {
                    await WithClient(async c =>
                    {
                        string resp = await ServerFixture.SendCommandAsync(c, "NOSUCHCMD", "arg");
                        Check.StringContains("unknown command", resp);
                    });
                }),

                Cases.Async(SuiteId, "multiple-clients", "The server serves multiple concurrent clients", async ct =>
                {
                    using (ServerFixture fixture = await ServerFixture.StartAsync())
                    using (RespRawClient c1 = await fixture.ConnectAsync())
                    using (RespRawClient c2 = await fixture.ConnectAsync())
                    {
                        await ServerFixture.SendCommandAsync(c1, "SET", "mc", "fromC1");
                        Check.Equal("$6\r\nfromC1\r\n", await ServerFixture.SendCommandAsync(c2, "GET", "mc"));
                    }
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Server end-to-end commands",
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
