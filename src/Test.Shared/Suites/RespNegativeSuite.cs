namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests the listener's handling of malformed, partial, and fragmented input, and verifies
    /// that bad input from one client never brings the server down for others.
    /// </summary>
    public static class RespNegativeSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "RespNegative";
        private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan NoEventWindow = TimeSpan.FromMilliseconds(800);

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the negative-path parsing test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Async(SuiteId, "invalid-prefix-no-event", "An unknown type prefix raises no data event", async ct =>
                {
                    await AssertNoEvent("@invalid\r\n");
                }),

                Cases.Async(SuiteId, "malformed-integer-no-event", "A non-numeric integer body raises no data event", async ct =>
                {
                    await AssertNoEvent(":notanumber\r\n");
                }),

                Cases.Async(SuiteId, "non-numeric-bulk-length-no-event", "A non-numeric bulk length raises no data event", async ct =>
                {
                    await AssertNoEvent("$abc\r\ndata\r\n");
                }),

                Cases.Async(SuiteId, "incomplete-bulk-buffered", "An incomplete bulk string is buffered and raises no event yet", async ct =>
                {
                    await AssertNoEvent("$10\r\nshort\r\n");
                }),

                Cases.Async(SuiteId, "partial-then-complete-bulk", "A bulk string split across writes is reassembled", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        await client.SendAsync("$6\r\nfoo");
                        bool premature = await harness.WaitForCountAsync(1, NoEventWindow);
                        Check.False(premature, "no event should fire before the bulk string is complete");

                        await client.SendAsync("bar\r\n");
                        RespDataReceivedEventArgs e = await harness.WaitForAsync(x => x.DataType == RespDataType.BulkString, EventTimeout);
                        Check.NotNull(e, "expected the reassembled bulk string");
                        Check.Equal("foobar", (string)e.Value);
                    }
                }),

                Cases.Async(SuiteId, "fragmented-simple-string", "A simple string split across writes is reassembled", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        await client.SendAsync("+O");
                        await client.SendAsync("K");
                        await client.SendAsync("\r\n");
                        RespDataReceivedEventArgs e = await harness.WaitForAsync(x => x.DataType == RespDataType.SimpleString, EventTimeout);
                        Check.NotNull(e, "expected the reassembled simple string");
                        Check.Equal("OK", (string)e.Value);
                    }
                }),

                Cases.Async(SuiteId, "multiple-messages-one-write", "Multiple messages in a single write each raise an event", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        await client.SendAsync("+ONE\r\n+TWO\r\n:3\r\n");
                        bool got = await harness.WaitForCountAsync(3, EventTimeout);
                        Check.True(got, "expected three parsed messages from a single write");
                    }
                }),

                Cases.Async(SuiteId, "bad-input-does-not-crash-server", "Malformed input from one client does not stop the server for another", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    {
                        using (RespRawClient bad = await harness.ConnectClientAsync())
                        {
                            await bad.SendAsync("@garbage\r\n");
                        }

                        using (RespRawClient good = await harness.ConnectClientAsync())
                        {
                            await good.SendAsync("+STILL-ALIVE\r\n");
                            RespDataReceivedEventArgs e = await harness.WaitForAsync(
                                x => x.DataType == RespDataType.SimpleString && (string)x.Value == "STILL-ALIVE", EventTimeout);
                            Check.NotNull(e, "server should still parse messages from a healthy client");
                        }
                    }
                }),

                Cases.Async(SuiteId, "empty-lines-resilience", "A stray CRLF does not stop the server for a subsequent client", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    {
                        using (RespRawClient noisy = await harness.ConnectClientAsync())
                        {
                            await noisy.SendAsync("\r\n\r\n");
                            await Task.Delay(100);
                        }

                        using (RespRawClient good = await harness.ConnectClientAsync())
                        {
                            await good.SendAsync("+OK\r\n");
                            RespDataReceivedEventArgs e = await harness.WaitForAsync(
                                x => x.DataType == RespDataType.SimpleString, EventTimeout);
                            Check.NotNull(e, "server should remain responsive after receiving stray CRLFs");
                        }
                    }
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "RESP negative and resilience",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        /// <summary>
        /// Sends the supplied wire data and asserts that no data event is raised within the window.
        /// </summary>
        /// <param name="wire">The wire data to send.</param>
        /// <returns>A task that completes when the assertion is made.</returns>
        private static async Task AssertNoEvent(string wire)
        {
            using (ListenerHarness harness = await ListenerHarness.StartAsync())
            using (RespRawClient client = await harness.ConnectClientAsync())
            {
                await client.SendAsync(wire);
                bool any = await harness.WaitForCountAsync(1, NoEventWindow);
                Check.False(any, "no data event should be raised for input [" + wire.Replace("\r", "\\r").Replace("\n", "\\n") + "]");
            }
        }

        #endregion

    }
}
