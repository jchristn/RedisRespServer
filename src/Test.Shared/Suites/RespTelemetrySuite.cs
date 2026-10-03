namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Proves the RedisRespServer library emits its documented metrics and spans: listener lifecycle, connection
    /// lifecycle and close reasons, bytes and messages, dispatch latency and spans, and every failure path.
    /// </summary>
    public static class RespTelemetrySuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "RespTelemetry";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the library telemetry suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "stable-names", "Meter and activity source names are the documented public contract", () =>
                {
                    Check.Equal("RedisRespServer", RespTelemetry.MeterName);
                    Check.Equal("RedisRespServer", RespTelemetry.ActivitySourceName);
                    Check.Equal("redisresp.connections.active", RespTelemetry.ConnectionsActive);
                    Check.Equal("redisresp.dispatch.duration", RespTelemetry.DispatchDuration);
                }),

                Cases.Async(SuiteId, "listener-lifecycle", "Starting and stopping a listener moves listeners.active up and down", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RespTelemetry.MeterName))
                    {
                        ListenerHarness harness = await ListenerHarness.StartAsync();
                        Check.True(capture.Find(RespTelemetry.ListenersActive).Any(m => m.Value == 1), "start should add 1");
                        harness.Dispose();
                        Check.True(capture.Find(RespTelemetry.ListenersActive).Any(m => m.Value == -1), "stop should add -1");
                    }
                }),

                Cases.Async(SuiteId, "connection-lifecycle", "A client connection records accepted, active, closed (client_closed), and duration", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RespTelemetry.MeterName))
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    {
                        RespRawClient client = await harness.ConnectClientAsync();
                        Check.True(await harness.WaitForConnectedCountAsync(1, Timeout));
                        Check.True(capture.Sum(RespTelemetry.ConnectionsAccepted) >= 1, "accepted should be counted");
                        client.Dispose();

                        Check.True(await capture.WaitUntilAsync(c => c.Sum(RespTelemetry.ConnectionsClosed,
                            RespTelemetry.AttributeCloseReason, RespTelemetry.CloseClientClosed) >= 1, Timeout), "closed{client_closed} should be counted");
                        Check.True(capture.Count(RespTelemetry.ConnectionDuration, RespTelemetry.AttributeCloseReason, RespTelemetry.CloseClientClosed) >= 1, "duration should be recorded");
                        Check.True(capture.Find(RespTelemetry.ConnectionsActive).Any(m => m.Value == 1), "active should rise on accept");
                        Check.True(capture.Find(RespTelemetry.ConnectionsActive).Any(m => m.Value == -1), "active should fall on close");
                        Check.Equal("s", capture.Find(RespTelemetry.ConnectionDuration).First().Unit);
                    }
                }),

                Cases.Async(SuiteId, "server-disconnect-reason", "A server-initiated disconnect is recorded as server_disconnect", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RespTelemetry.MeterName))
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        Check.True(await harness.WaitForConnectedCountAsync(1, Timeout));
                        Guid guid = harness.Listener.RetrieveClients().First().GUID;
                        harness.Listener.DisconnectClientByGuid(guid);

                        Check.True(await capture.WaitUntilAsync(c => c.Sum(RespTelemetry.ConnectionsClosed,
                            RespTelemetry.AttributeCloseReason, RespTelemetry.CloseServerDisconnect) >= 1, Timeout), "closed{server_disconnect} should be counted");
                    }
                }),

                Cases.Async(SuiteId, "message-metrics", "A RESP array records bytes, message count by type and protocol, size, and dispatch latency", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RespTelemetry.MeterName))
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        string frame = "*2\r\n$3\r\nGET\r\n$3\r\nkey\r\n";
                        await client.SendAsync(frame);
                        Check.NotNull(await harness.WaitForAsync(e => e.DataType == RespDataType.Array, Timeout));

                        Check.True(await capture.WaitUntilAsync(c => c.Count(RespTelemetry.DispatchDuration) >= 1, Timeout));
                        Check.True(capture.Sum(RespTelemetry.BytesReceived) >= frame.Length, "bytes received should be counted");
                        Check.True(capture.Sum(RespTelemetry.MessagesReceived,
                            RespTelemetry.AttributeRespType, "Array", RespTelemetry.AttributeRespProtocol, "RESP2") >= 1, "message should be counted by type");
                        Check.True(capture.Find(RespTelemetry.MessageSize, RespTelemetry.AttributeRespType, "Array").Any(m => m.Value == frame.Length), "message size should be recorded");
                        Check.True(capture.Count(RespTelemetry.DispatchDuration,
                            RespTelemetry.AttributeRespType, "Array", RespTelemetry.AttributeOutcome, RespTelemetry.OutcomeOk) >= 1, "dispatch duration should be recorded");
                    }
                }),

                Cases.Async(SuiteId, "dispatch-span", "Each dispatched message opens a server span with type, protocol, size, and Ok status", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RespTelemetry.ActivitySourceName))
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        await client.SendAsync("+hello\r\n");
                        Check.NotNull(await harness.WaitForAsync(e => e.DataType == RespDataType.SimpleString, Timeout));
                        Check.True(await capture.WaitUntilAsync(c => c.FindActivities("resp.dispatch SimpleString").Any(), Timeout), "dispatch span should be emitted");

                        Activity span = capture.FindActivities("resp.dispatch SimpleString").First();
                        Check.Equal(ActivityKind.Server, span.Kind);
                        Check.Equal(ActivityStatusCode.Ok, span.Status);
                        Check.Equal("RESP2", span.GetTagItem(RespTelemetry.AttributeRespProtocol) as string);
                        Check.NotNull(span.GetTagItem(RespTelemetry.AttributeClientId));
                        Check.NotNull(span.GetTagItem(RespTelemetry.AttributeClientAddress));
                        Check.Null(span.Parent, "dispatch spans are trace roots");
                    }
                }),

                Cases.Async(SuiteId, "dispatch-handler-exception", "A throwing subscriber records outcome=error and an Error span with an exception event", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RespTelemetry.MeterName))
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        harness.Listener.IntegerReceived += (s, e) => throw new InvalidOperationException("handler failure");
                        await client.SendAsync(":42\r\n");

                        Check.True(await capture.WaitUntilAsync(c => c.Count(RespTelemetry.DispatchDuration,
                            RespTelemetry.AttributeRespType, "Integer", RespTelemetry.AttributeOutcome, RespTelemetry.OutcomeError) >= 1, Timeout), "dispatch error should be recorded");
                    }

                    using (TelemetryCapture capture = new TelemetryCapture(RespTelemetry.ActivitySourceName))
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        harness.Listener.IntegerReceived += (s, e) => throw new InvalidOperationException("handler failure");
                        await client.SendAsync(":42\r\n");
                        Check.True(await capture.WaitUntilAsync(c => c.FindActivities("resp.dispatch Integer").Any(), Timeout));

                        Activity span = capture.FindActivities("resp.dispatch Integer").First();
                        Check.Equal(ActivityStatusCode.Error, span.Status);
                        Check.Equal("InvalidOperationException", span.GetTagItem(RespTelemetry.AttributeErrorType) as string);
                        Check.True(span.Events.Any(ev => ev.Name == "exception"), "exception event should be attached");
                    }
                }),

                Cases.Async(SuiteId, "unknown-prefix", "Input with an unknown type prefix records parse.errors{unknown_prefix} once per connection", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RespTelemetry.MeterName))
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        await client.SendAsync("?garbage\r\n");
                        await Task.Delay(100);
                        await client.SendAsync("more\r\n");

                        Check.True(await capture.WaitUntilAsync(c => c.Sum(RespTelemetry.ParseErrors,
                            RespTelemetry.AttributeErrorType, RespTelemetry.ErrorUnknownPrefix) >= 1, Timeout), "parse error should be recorded");
                        await Task.Delay(100);
                        Check.Equal(1.0, capture.Sum(RespTelemetry.ParseErrors, RespTelemetry.AttributeErrorType, RespTelemetry.ErrorUnknownPrefix), "reported once per connection");
                    }
                }),

                Cases.Async(SuiteId, "unhandled-message", "A parsed message that matches no dispatch path records outcome=unhandled with type Unknown", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RespTelemetry.MeterName))
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        // An integer frame whose payload is not numeric parses but is never dispatched.
                        await client.SendAsync(":abc\r\n");

                        Check.True(await capture.WaitUntilAsync(c => c.Count(RespTelemetry.DispatchDuration,
                            RespTelemetry.AttributeRespType, RespTelemetry.Unknown, RespTelemetry.AttributeOutcome, RespTelemetry.OutcomeUnhandled) >= 1, Timeout), "unhandled dispatch should be recorded");
                    }
                }),

                Cases.Async(SuiteId, "labels-bounded", "No library metric carries a client id or payload label", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RespTelemetry.MeterName))
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        await client.SendAsync("*2\r\n$3\r\nSET\r\n$6\r\nsecret\r\n");
                        Check.True(await capture.WaitUntilAsync(c => c.Count(RespTelemetry.DispatchDuration) >= 1, Timeout));

                        HashSet<string> allowed = new HashSet<string>
                        {
                            RespTelemetry.AttributeRespType, RespTelemetry.AttributeRespProtocol, RespTelemetry.AttributeOutcome,
                            RespTelemetry.AttributeCloseReason, RespTelemetry.AttributeErrorType
                        };
                        foreach (CapturedMeasurement m in capture.Measurements)
                        {
                            foreach (KeyValuePair<string, string> tag in m.Tags)
                            {
                                Check.True(allowed.Contains(tag.Key), "unexpected label " + tag.Key + " on " + m.Instrument);
                                Check.False(tag.Value != null && tag.Value.Contains("secret"), "payload leaked into a label");
                            }
                        }
                    }
                }),

                Cases.Async(SuiteId, "no-listener", "With no telemetry listener attached, traffic is handled normally and nothing throws", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        await client.SendAsync("*1\r\n$4\r\nPING\r\n");
                        Check.NotNull(await harness.WaitForAsync(e => e.DataType == RespDataType.Array, Timeout));
                        await client.SendAsync("?bad\r\n");
                        Check.Equal(0, harness.Errors.Count, "no errors should be raised by instrumentation");
                    }
                }),
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "RedisRespServer library telemetry",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
