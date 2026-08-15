namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests that the listener parses each RESP2 wire format into the correct typed event and value.
    /// </summary>
    public static class Resp2ParsingSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "Resp2Parsing";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the RESP2 parsing test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Async(SuiteId, "simple-string", "Simple string (+) parses to a SimpleString event", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("+OK\r\n", RespDataType.SimpleString);
                    Check.Equal("OK", (string)e.Value);
                    Check.Equal(RespVersionEnum.RESP2, e.ProtocolVersion);
                }),

                Cases.Async(SuiteId, "error", "Error (-) parses to an Error event", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("-ERR unknown command\r\n", RespDataType.Error);
                    Check.Equal("ERR unknown command", (string)e.Value);
                }),

                Cases.Async(SuiteId, "integer-positive", "Positive integer (:) parses to a long", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive(":1000\r\n", RespDataType.Integer);
                    Check.Equal(1000L, (long)e.Value);
                }),

                Cases.Async(SuiteId, "integer-negative", "Negative integer (:) parses to a long", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive(":-42\r\n", RespDataType.Integer);
                    Check.Equal(-42L, (long)e.Value);
                }),

                Cases.Async(SuiteId, "integer-zero", "Zero integer (:) parses to a long", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive(":0\r\n", RespDataType.Integer);
                    Check.Equal(0L, (long)e.Value);
                }),

                Cases.Async(SuiteId, "bulk-string", "Bulk string ($) parses to its payload", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("$6\r\nfoobar\r\n", RespDataType.BulkString);
                    Check.Equal("foobar", (string)e.Value);
                }),

                Cases.Async(SuiteId, "bulk-string-empty", "Empty bulk string ($0) parses to an empty string", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("$0\r\n\r\n", RespDataType.BulkString);
                    Check.Equal("", (string)e.Value);
                }),

                Cases.Async(SuiteId, "bulk-string-binary", "Bulk string preserves binary bytes 1:1", async ct =>
                {
                    // Bytes 0x00..0x04 embedded in a 5-byte bulk string.
                    string wire = "$5\r\n\u0000\u0001\u0002\u0003\u0004\r\n";
                    RespDataReceivedEventArgs e = await SendAndReceive(wire, RespDataType.BulkString);
                    Check.NotNull(e.RawBytes);
                    Check.Equal(5, e.RawBytes.Length);
                    for (int i = 0; i < 5; i++) Check.Equal((byte)i, e.RawBytes[i]);
                }),

                Cases.Async(SuiteId, "null-bulk-string", "Null bulk string ($-1) parses to a Null event", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("$-1\r\n", RespDataType.Null);
                    Check.Null(e.Value);
                }),

                Cases.Async(SuiteId, "array", "Array (*) parses to an object array of its elements", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n", RespDataType.Array);
                    object[] arr = (object[])e.Value;
                    Check.Equal(2, arr.Length);
                    Check.Equal("foo", (string)arr[0]);
                    Check.Equal("bar", (string)arr[1]);
                }),

                Cases.Async(SuiteId, "array-mixed", "Array with mixed element types parses each element", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("*2\r\n:7\r\n$3\r\nabc\r\n", RespDataType.Array);
                    object[] arr = (object[])e.Value;
                    Check.Equal(2, arr.Length);
                    Check.Equal(7L, (long)arr[0]);
                    Check.Equal("abc", (string)arr[1]);
                }),

                Cases.Async(SuiteId, "array-nested", "Nested arrays parse recursively", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("*2\r\n*1\r\n:1\r\n$3\r\nabc\r\n", RespDataType.Array);
                    object[] arr = (object[])e.Value;
                    Check.Equal(2, arr.Length);
                    object[] inner = (object[])arr[0];
                    Check.Equal(1, inner.Length);
                    Check.Equal(1L, (long)inner[0]);
                    Check.Equal("abc", (string)arr[1]);
                }),

                Cases.Async(SuiteId, "null-array", "Null array (*-1) parses to a Null event", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("*-1\r\n", RespDataType.Null);
                    Check.Null(e.Value);
                }),

                Cases.Async(SuiteId, "client-guid-populated", "Parsed events carry the originating client GUID", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("+PING\r\n", RespDataType.SimpleString);
                    Check.NotEqual(Guid.Empty, e.ClientGUID);
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "RESP2 protocol parsing",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        /// <summary>
        /// Starts a listener, sends the wire data from a fresh client, and returns the first
        /// captured event of the expected type.
        /// </summary>
        /// <param name="wire">The exact bytes (as a Latin1 string) to send.</param>
        /// <param name="expected">The RESP data type expected to be raised.</param>
        /// <returns>The captured event.</returns>
        private static async Task<RespDataReceivedEventArgs> SendAndReceive(string wire, RespDataType expected)
        {
            using (ListenerHarness harness = await ListenerHarness.StartAsync())
            using (RespRawClient client = await harness.ConnectClientAsync())
            {
                await client.SendAsync(wire);
                RespDataReceivedEventArgs e = await harness.WaitForAsync(x => x.DataType == expected, Timeout);
                Check.NotNull(e, "Expected a " + expected + " event for wire [" + Readable(wire) + "]");
                return e;
            }
        }

        /// <summary>
        /// Produces a readable form of wire data for failure messages.
        /// </summary>
        /// <param name="wire">The wire data.</param>
        /// <returns>A CRLF-escaped representation.</returns>
        private static string Readable(string wire)
        {
            return wire.Replace("\r", "\\r").Replace("\n", "\\n");
        }

        #endregion

    }
}
