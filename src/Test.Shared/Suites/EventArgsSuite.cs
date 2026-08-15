namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the public event-argument classes and their default values.
    /// </summary>
    public static class EventArgsSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "EventArgs";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the event-argument test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "resp-data-defaults", "RespDataReceivedEventArgs defaults to RESP2 with a recent timestamp", () =>
                {
                    RespDataReceivedEventArgs e = new RespDataReceivedEventArgs();
                    Check.Equal(RespVersionEnum.RESP2, e.ProtocolVersion);
                    Check.True(e.Timestamp <= DateTime.UtcNow.AddSeconds(1), "Timestamp should be initialized to now");
                    Check.True(e.Timestamp > DateTime.UtcNow.AddMinutes(-1), "Timestamp should be recent");
                }),

                Cases.Sync(SuiteId, "resp-data-roundtrip", "RespDataReceivedEventArgs stores assigned values", () =>
                {
                    Guid g = Guid.NewGuid();
                    byte[] raw = new byte[] { 1, 2, 3 };
                    RespDataReceivedEventArgs e = new RespDataReceivedEventArgs
                    {
                        DataType = RespDataType.BulkString,
                        Value = "hello",
                        RawData = "$5\r\nhello\r\n",
                        ClientGUID = g,
                        RawBytes = raw,
                        MessageBytes = raw,
                        ProtocolVersion = RespVersionEnum.RESP3
                    };
                    Check.Equal(RespDataType.BulkString, e.DataType);
                    Check.Equal("hello", (string)e.Value);
                    Check.Equal(g, e.ClientGUID);
                    Check.Equal(RespVersionEnum.RESP3, e.ProtocolVersion);
                    Check.SequenceEqual(raw, e.RawBytes);
                }),

                Cases.Sync(SuiteId, "client-connected-defaults", "ClientConnectedEventArgs initializes ConnectedUtc", () =>
                {
                    ClientConnectedEventArgs e = new ClientConnectedEventArgs();
                    Check.True(e.ConnectedUtc <= DateTime.UtcNow.AddSeconds(1));
                    Check.True(e.ConnectedUtc > DateTime.UtcNow.AddMinutes(-1));
                }),

                Cases.Sync(SuiteId, "client-disconnected-defaults", "ClientDisconnectedEventArgs initializes DisconnectedAt and stores a reason", () =>
                {
                    ClientDisconnectedEventArgs e = new ClientDisconnectedEventArgs { Reason = "test" };
                    Check.True(e.DisconnectedAt <= DateTime.UtcNow.AddSeconds(1));
                    Check.Equal("test", e.Reason);
                }),

                Cases.Sync(SuiteId, "error-args-nullable-guid", "ErrorEventArgs supports a null client GUID for server-wide errors", () =>
                {
                    RedisResp.ErrorEventArgs e = new RedisResp.ErrorEventArgs { Message = "boom" };
                    Check.Null(e.GUID);
                    Check.Equal("boom", e.Message);

                    Guid g = Guid.NewGuid();
                    RedisResp.ErrorEventArgs e2 = new RedisResp.ErrorEventArgs { GUID = g, Exception = new InvalidOperationException("x") };
                    Check.True(e2.GUID.HasValue);
                    Check.Equal(g, e2.GUID.Value);
                    Check.NotNull(e2.Exception);
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Event argument classes",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
