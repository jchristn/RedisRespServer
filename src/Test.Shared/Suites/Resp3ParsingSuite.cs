namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests that the listener parses each RESP3 wire format into the correct typed event,
    /// value, and protocol version.
    /// </summary>
    public static class Resp3ParsingSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "Resp3Parsing";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the RESP3 parsing test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Async(SuiteId, "double", "Double (,) parses to a double with RESP3 version", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive(",10\r\n", RespDataType.Double);
                    Check.Equal(10.0, (double)e.Value);
                    Check.Equal(RespVersionEnum.RESP3, e.ProtocolVersion);
                }),

                Cases.Async(SuiteId, "boolean-true", "Boolean (#t) parses to true", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("#t\r\n", RespDataType.Boolean);
                    Check.Equal(true, (bool)e.Value);
                    Check.Equal(RespVersionEnum.RESP3, e.ProtocolVersion);
                }),

                Cases.Async(SuiteId, "boolean-false", "Boolean (#f) parses to false", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("#f\r\n", RespDataType.Boolean);
                    Check.Equal(false, (bool)e.Value);
                }),

                Cases.Async(SuiteId, "big-number", "Big number (() parses to its string content", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("(3492890328409238509324850943850943825024385\r\n", RespDataType.BigNumber);
                    Check.Equal("3492890328409238509324850943850943825024385", (string)e.Value);
                    Check.Equal(RespVersionEnum.RESP3, e.ProtocolVersion);
                }),

                Cases.Async(SuiteId, "verbatim-string", "Verbatim string (=) parses to its payload", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("=15\r\ntxt:hello world\r\n", RespDataType.VerbatimString);
                    Check.Equal("txt:hello world", (string)e.Value);
                    Check.Equal(RespVersionEnum.RESP3, e.ProtocolVersion);
                }),

                Cases.Async(SuiteId, "map", "Map (%) parses to a flat object array of key/value elements", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("%2\r\n+a\r\n:1\r\n+b\r\n:2\r\n", RespDataType.Map);
                    object[] arr = (object[])e.Value;
                    Check.Equal(4, arr.Length);
                    Check.Equal("a", (string)arr[0]);
                    Check.Equal(1L, (long)arr[1]);
                    Check.Equal("b", (string)arr[2]);
                    Check.Equal(2L, (long)arr[3]);
                    Check.Equal(RespVersionEnum.RESP3, e.ProtocolVersion);
                }),

                Cases.Async(SuiteId, "set", "Set (~) parses to an object array of elements", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("~2\r\n+a\r\n+b\r\n", RespDataType.Set);
                    object[] arr = (object[])e.Value;
                    Check.Equal(2, arr.Length);
                    Check.Equal("a", (string)arr[0]);
                    Check.Equal("b", (string)arr[1]);
                    Check.Equal(RespVersionEnum.RESP3, e.ProtocolVersion);
                }),

                Cases.Async(SuiteId, "push", "Push (>) parses to an object array with RESP3 version", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive(">2\r\n+message\r\n+payload\r\n", RespDataType.Push);
                    object[] arr = (object[])e.Value;
                    Check.Equal(2, arr.Length);
                    Check.Equal("message", (string)arr[0]);
                    Check.Equal(RespVersionEnum.RESP3, e.ProtocolVersion);
                }),

                Cases.Async(SuiteId, "attribute", "Attribute (|) parses to a flat object array", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("|1\r\n+key\r\n+value\r\n", RespDataType.Attribute);
                    object[] arr = (object[])e.Value;
                    Check.Equal(2, arr.Length);
                    Check.Equal("key", (string)arr[0]);
                    Check.Equal("value", (string)arr[1]);
                    Check.Equal(RespVersionEnum.RESP3, e.ProtocolVersion);
                }),

                Cases.Async(SuiteId, "blob-error", "Blob error (!) parses to a BlobError event", async ct =>
                {
                    RespDataReceivedEventArgs e = await SendAndReceive("!10\r\nSOME ERROR\r\n", RespDataType.BlobError);
                    Check.Equal(RespVersionEnum.RESP3, e.ProtocolVersion);
                    Check.NotNull(e.Value);
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "RESP3 protocol parsing",
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
                Check.NotNull(e, "Expected a " + expected + " event");
                return e;
            }
        }

        #endregion

    }
}
