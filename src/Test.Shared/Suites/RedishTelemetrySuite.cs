namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Radiant;
    using Redish.Server.Models;
    using Redish.Server.Settings;
    using Redish.Server.Telemetry;
    using RedisResp;
    using SyslogLogging;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Proves the Redish server emits its documented metrics and spans: the command workflow and its stages,
    /// error replies and exceptions, keyspace lookups, AUTH, gauges, the expiration sweep job (success, failure,
    /// active and passive expiry), and the Radiant host (scrape endpoint, resilience, settings, log bridge).
    /// </summary>
    public static class RedishTelemetrySuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "RedishTelemetry";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the Redish server telemetry suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "stable-names", "Meter and activity source names are the documented public contract", () =>
                {
                    Check.Equal("Redish.Server", RedishTelemetry.MeterName);
                    Check.Equal("Redish.Server", RedishTelemetry.ActivitySourceName);
                    Check.Equal("redish.command.duration", RedishTelemetry.CommandDuration);
                    Check.Equal("redish.expiration.last_success", RedishTelemetry.ExpirationLastSuccess);
                }),

                Cases.Async(SuiteId, "command-metrics", "Commands record count, latency, per-stage latency, and reply size by operation", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName))
                    using (ServerFixture server = await ServerFixture.StartAsync())
                    using (RespRawClient client = await server.ConnectAsync())
                    {
                        Check.Equal("+OK\r\n", await ServerFixture.SendCommandAsync(client, "SET", "k", "hello"));
                        Check.Equal("$5\r\nhello\r\n", await ServerFixture.SendCommandAsync(client, "GET", "k"));

                        Check.True(await capture.WaitUntilAsync(c => c.Sum(RedishTelemetry.Commands,
                            RedishTelemetry.AttributeOperation, "GET", RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeOk) >= 1, Timeout));
                        Check.True(capture.Sum(RedishTelemetry.Commands, RedishTelemetry.AttributeOperation, "SET", RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeOk) >= 1);
                        Check.True(capture.Count(RedishTelemetry.CommandDuration, RedishTelemetry.AttributeOperation, "GET") >= 1, "command duration");
                        Check.True(capture.Count(RedishTelemetry.CommandStageDuration,
                            RedishTelemetry.AttributeStage, RedishTelemetry.StageExecute, RedishTelemetry.AttributeOperation, "GET") >= 1, "execute stage");
                        Check.True(capture.Count(RedishTelemetry.CommandStageDuration,
                            RedishTelemetry.AttributeStage, RedishTelemetry.StageRespond, RedishTelemetry.AttributeOperation, "GET") >= 1, "respond stage");
                        Check.True(capture.Find(RedishTelemetry.ResponseSize, RedishTelemetry.AttributeOperation, "GET").Any(m => m.Value == 11), "reply size");
                    }
                }),

                Cases.Async(SuiteId, "error-reply", "An error reply records outcome=error and command.errors by error code", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName))
                    using (ServerFixture server = await ServerFixture.StartAsync())
                    using (RespRawClient client = await server.ConnectAsync())
                    {
                        await ServerFixture.SendCommandAsync(client, "SET", "k", "v");
                        string reply = await ServerFixture.SendCommandAsync(client, "LPUSH", "k", "x");
                        Check.StringContains("WRONGTYPE", reply);

                        Check.True(await capture.WaitUntilAsync(c => c.Sum(RedishTelemetry.CommandErrors,
                            RedishTelemetry.AttributeOperation, "LPUSH", RedishTelemetry.AttributeErrorType, "WRONGTYPE") >= 1, Timeout), "error by type");
                        Check.True(capture.Sum(RedishTelemetry.Commands,
                            RedishTelemetry.AttributeOperation, "LPUSH", RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeError) >= 1, "outcome error");
                    }
                }),

                Cases.Async(SuiteId, "unknown-command-bounded", "Unsupported command names are reported as UNKNOWN, never as the raw name", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName))
                    using (ServerFixture server = await ServerFixture.StartAsync())
                    using (RespRawClient client = await server.ConnectAsync())
                    {
                        await ServerFixture.SendCommandAsync(client, "NOTACOMMAND123");

                        Check.True(await capture.WaitUntilAsync(c => c.Sum(RedishTelemetry.Commands,
                            RedishTelemetry.AttributeOperation, RedishTelemetry.OperationUnknown,
                            RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeUnknownCommand) >= 1, Timeout));
                        Check.False(capture.Measurements.Any(m => m.Tags.Values.Any(v => v != null && v.Contains("NOTACOMMAND123"))), "raw command name must not become a label");
                    }
                }),

                Cases.Async(SuiteId, "command-exception", "A command that throws records outcome=exception, the exception type, and an Error span", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName, RedishTelemetry.ActivitySourceName))
                    using (ServerFixture server = await ServerFixture.StartAsync())
                    using (RespRawClient client = await server.ConnectAsync())
                    {
                        server.Server.RespInterface.Authenticate = (u, p) => throw new InvalidOperationException("authenticator unavailable");
                        string reply = await ServerFixture.SendCommandAsync(client, "AUTH", "pw");
                        Check.StringContains("internal server error", reply);

                        Check.True(await capture.WaitUntilAsync(c => c.Sum(RedishTelemetry.CommandErrors,
                            RedishTelemetry.AttributeOperation, "AUTH", RedishTelemetry.AttributeErrorType, "InvalidOperationException") >= 1, Timeout));
                        Check.True(capture.Sum(RedishTelemetry.Commands,
                            RedishTelemetry.AttributeOperation, "AUTH", RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeException) >= 1);

                        Check.True(await capture.WaitUntilAsync(c => c.FindActivities("redish.command AUTH").Any(), Timeout));
                        Activity span = capture.FindActivities("redish.command AUTH").First();
                        Check.Equal(ActivityStatusCode.Error, span.Status);
                        Check.True(span.Events.Any(e => e.Name == "exception"), "exception event");
                        Activity execute = capture.FindActivities("stage:execute").FirstOrDefault(a => a.ParentSpanId == span.SpanId);
                        Check.NotNull(execute, "execute stage span");
                        Check.Equal(ActivityStatusCode.Error, execute.Status);
                    }
                }),

                Cases.Async(SuiteId, "span-hierarchy", "resp.dispatch, redish.command, and stage spans form one trace", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.ActivitySourceName, RespTelemetry.ActivitySourceName))
                    using (ServerFixture server = await ServerFixture.StartAsync())
                    using (RespRawClient client = await server.ConnectAsync())
                    {
                        await ServerFixture.SendCommandAsync(client, "GET", "missing");
                        Check.True(await capture.WaitUntilAsync(c => c.FindActivities("redish.command GET").Any()
                            && c.FindActivities("stage:respond").Any(), Timeout));

                        Activity command = capture.FindActivities("redish.command GET").First();
                        Activity dispatch = capture.FindActivities("resp.dispatch Array").FirstOrDefault(a => a.SpanId == command.ParentSpanId);
                        Check.NotNull(dispatch, "command span should be a child of the dispatch span");
                        Check.Equal(dispatch.TraceId, command.TraceId);
                        Check.Equal("GET", command.GetTagItem(RedishTelemetry.AttributeOperation) as string);
                        Check.Equal("redis", command.GetTagItem(RedishTelemetry.AttributeDbSystem) as string);
                        Check.Equal(ActivityStatusCode.Ok, command.Status);

                        List<Activity> stages = capture.Activities.Where(a => a.ParentSpanId == command.SpanId).ToList();
                        Check.True(stages.Any(a => a.DisplayName == "stage:execute"), "execute stage child");
                        Check.True(stages.Any(a => a.DisplayName == "stage:respond"), "respond stage child");
                    }
                }),

                Cases.Async(SuiteId, "keyspace-lookups", "GET and HGET record keyspace hits and misses", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName))
                    using (ServerFixture server = await ServerFixture.StartAsync())
                    using (RespRawClient client = await server.ConnectAsync())
                    {
                        await ServerFixture.SendCommandAsync(client, "SET", "present", "v");
                        await ServerFixture.SendCommandAsync(client, "GET", "present");
                        await ServerFixture.SendCommandAsync(client, "GET", "absent");
                        await ServerFixture.SendCommandAsync(client, "HGET", "nohash", "f");

                        Check.True(await capture.WaitUntilAsync(c => c.Sum(RedishTelemetry.KeyspaceLookups,
                            RedishTelemetry.AttributeOperation, "HGET", RedishTelemetry.AttributeLookupResult, RedishTelemetry.LookupMiss) >= 1, Timeout));
                        Check.True(capture.Sum(RedishTelemetry.KeyspaceLookups,
                            RedishTelemetry.AttributeOperation, "GET", RedishTelemetry.AttributeLookupResult, RedishTelemetry.LookupHit) >= 1, "GET hit");
                        Check.True(capture.Sum(RedishTelemetry.KeyspaceLookups,
                            RedishTelemetry.AttributeOperation, "GET", RedishTelemetry.AttributeLookupResult, RedishTelemetry.LookupMiss) >= 1, "GET miss");
                    }
                }),

                Cases.Async(SuiteId, "auth-outcomes", "AUTH records success, failure, not_required, and invalid_arguments", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName))
                    using (ServerFixture server = await ServerFixture.StartAsync())
                    using (RespRawClient client = await server.ConnectAsync())
                    {
                        await ServerFixture.SendCommandAsync(client, "AUTH", "anything");
                        await ServerFixture.SendCommandAsync(client, "AUTH");
                        server.Server.RespInterface.Authenticate = (u, p) => p == "good";
                        await ServerFixture.SendCommandAsync(client, "AUTH", "bad");
                        await ServerFixture.SendCommandAsync(client, "AUTH", "user", "good");

                        Check.True(await capture.WaitUntilAsync(c => c.Sum(RedishTelemetry.AuthAttempts,
                            RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeSuccess) >= 1, Timeout), "success");
                        Check.True(capture.Sum(RedishTelemetry.AuthAttempts, RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeFailure) >= 1, "failure");
                        Check.True(capture.Sum(RedishTelemetry.AuthAttempts, RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeNotRequired) >= 1, "not_required");
                        Check.True(capture.Sum(RedishTelemetry.AuthAttempts, RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeInvalidArguments) >= 1, "invalid_arguments");
                        Check.True(capture.Sum(RedishTelemetry.CommandErrors,
                            RedishTelemetry.AttributeOperation, "AUTH", RedishTelemetry.AttributeErrorType, "WRONGPASS") >= 1, "WRONGPASS error reply");
                    }
                }),

                Cases.Async(SuiteId, "echo-binary-path", "ECHO (raw byte reply path) records respond stage, reply size, and outcome", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName))
                    using (ServerFixture server = await ServerFixture.StartAsync())
                    using (RespRawClient client = await server.ConnectAsync())
                    {
                        Check.Equal("$2\r\nhi\r\n", await ServerFixture.SendCommandAsync(client, "ECHO", "hi"));
                        Check.True(await capture.WaitUntilAsync(c => c.Sum(RedishTelemetry.Commands,
                            RedishTelemetry.AttributeOperation, "ECHO", RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeOk) >= 1, Timeout));
                        Check.True(capture.Count(RedishTelemetry.CommandStageDuration,
                            RedishTelemetry.AttributeStage, RedishTelemetry.StageRespond, RedishTelemetry.AttributeOperation, "ECHO") >= 1);
                        Check.True(capture.Find(RedishTelemetry.ResponseSize, RedishTelemetry.AttributeOperation, "ECHO").Any(m => m.Value == 8));
                    }
                }),

                Cases.Async(SuiteId, "gauges", "Gauges report keys, clients by protocol, build info, database count, and uptime", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName))
                    using (ServerFixture server = await ServerFixture.StartAsync())
                    using (RespRawClient client = await server.ConnectAsync())
                    {
                        await ServerFixture.SendCommandAsync(client, "SET", "g", "1");
                        await ServerFixture.SendCommandAsync(client, "HELLO", "3");
                        capture.CollectObservables();

                        Check.True(capture.Find(RedishTelemetry.StorageKeys).Any(m => m.Value >= 1), "storage keys");
                        Check.True(capture.Find(RedishTelemetry.Clients, RedishTelemetry.AttributeRespProtocol, "RESP3").Any(m => m.Value >= 1), "RESP3 client");
                        CapturedMeasurement build = capture.Find(RedishTelemetry.BuildInfo).FirstOrDefault();
                        Check.NotNull(build, "build info");
                        Check.Equal(1.0, build.Value);
                        Check.Equal("Ram", build.Tags[RedishTelemetry.AttributeStorageMode]);
                        Check.True(build.Tags.ContainsKey(RedishTelemetry.AttributeServiceVersion));
                        Check.True(capture.Find(RedishTelemetry.ConfigDatabases).Any(m => m.Value == 16), "databases");
                        Check.True(capture.Find(RedishTelemetry.Uptime).Any(m => m.Value > 0), "uptime");
                        Check.True(capture.Find(RedishTelemetry.ExpirationLastSuccess).Any(), "last success gauge");
                    }
                }),

                Cases.Async(SuiteId, "expiration-end-to-end", "EXPIRE leads to an active sweep eviction with per-stage metrics, last-success, and a sweep trace", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName, RedishTelemetry.ActivitySourceName))
                    using (ServerFixture server = await ServerFixture.StartAsync())
                    using (RespRawClient client = await server.ConnectAsync())
                    {
                        await ServerFixture.SendCommandAsync(client, "SET", "ttl-key", "v");
                        await ServerFixture.SendCommandAsync(client, "EXPIRE", "ttl-key", "1");

                        Check.True(await capture.WaitUntilAsync(c => c.Sum(RedishTelemetry.ExpirationKeysExpired,
                            RedishTelemetry.AttributeTrigger, RedishTelemetry.TriggerActive) >= 1, TimeSpan.FromSeconds(6)), "active expiry");
                        Check.True(capture.Sum(RedishTelemetry.ExpirationSweeps, RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeSuccess) >= 1, "sweep success");
                        foreach (string stage in new[] { RedishTelemetry.StageQueued, RedishTelemetry.StageSnapshot, RedishTelemetry.StageEvict })
                        {
                            Check.True(capture.Sum(RedishTelemetry.ExpirationStageEvents, RedishTelemetry.AttributeStage, stage) >= 1, "stage counter " + stage);
                            Check.True(capture.Count(RedishTelemetry.ExpirationStageDuration, RedishTelemetry.AttributeStage, stage) >= 1, "stage histogram " + stage);
                        }
                        Check.True(capture.Sum(RedishTelemetry.ExpirationKeysScanned) >= 1, "keys scanned");

                        capture.CollectObservables();
                        Check.True(capture.Find(RedishTelemetry.ExpirationLastSuccess).Any(m => m.Value > 1_600_000_000), "last success timestamp");

                        Check.True(await capture.WaitUntilAsync(c => c.FindActivities(RedishTelemetry.SpanExpirationSweep).Any(), Timeout), "sweep trace");
                        Activity sweep = capture.FindActivities(RedishTelemetry.SpanExpirationSweep).First();
                        Check.Equal(default(ActivitySpanId), sweep.ParentSpanId, "sweep is a root span");
                        List<string> children = capture.Activities.Where(a => a.ParentSpanId == sweep.SpanId).Select(a => a.DisplayName).ToList();
                        Check.Contains("stage:queued", children);
                        Check.Contains("stage:snapshot", children);
                        Check.Contains("stage:evict", children);
                    }
                }),

                Cases.Async(SuiteId, "expiration-passive", "Reading a key after its TTL elapsed records a passive expiry", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName))
                    using (SweepableStorage storage = new SweepableStorage())
                    {
                        StringValue value = new StringValue("v");
                        value.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
                        storage["stale"] = value;

                        Check.False(storage.TryGetValue("stale", out RedisValue _), "expired key is not returned");
                        Check.True(capture.Sum(RedishTelemetry.ExpirationKeysExpired,
                            RedishTelemetry.AttributeTrigger, RedishTelemetry.TriggerPassive) >= 1, "passive expiry");
                        await Task.CompletedTask;
                    }
                }),

                Cases.Async(SuiteId, "expiration-idle-no-trace", "A sweep that expires nothing records metrics but emits no trace", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName, RedishTelemetry.ActivitySourceName))
                    using (SweepableStorage storage = new SweepableStorage())
                    {
                        storage["live"] = new StringValue("v");
                        await storage.RunSweepAsync();

                        Check.True(capture.Sum(RedishTelemetry.ExpirationSweeps, RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeSuccess) >= 1);
                        Check.False(capture.FindActivities(RedishTelemetry.SpanExpirationSweep)
                            .Any(a => Convert.ToInt64(a.GetTagItem(RedishTelemetry.AttributeKeysExpired)) == 0 && a.Status != ActivityStatusCode.Error),
                            "idle sweeps must not be traced");
                    }
                }),

                Cases.Async(SuiteId, "expiration-failure", "A failing sweep records outcome=failure, a failed snapshot stage, and an Error trace", async ct =>
                {
                    using (TelemetryCapture capture = new TelemetryCapture(RedishTelemetry.MeterName, RedishTelemetry.ActivitySourceName))
                    using (SweepableStorage storage = new SweepableStorage())
                    {
                        storage.FailKeyEnumeration = true;
                        await storage.RunSweepAsync();

                        Check.True(capture.Sum(RedishTelemetry.ExpirationSweeps, RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeFailure) >= 1, "sweep failure");
                        Check.True(capture.Sum(RedishTelemetry.ExpirationStageEvents,
                            RedishTelemetry.AttributeStage, RedishTelemetry.StageSnapshot, RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeFailure) >= 1, "snapshot failure");
                        Check.Equal(0, capture.Count(RedishTelemetry.ExpirationStageEvents,
                            RedishTelemetry.AttributeStage, RedishTelemetry.StageEvict, RedishTelemetry.AttributeOutcome, RedishTelemetry.OutcomeFailure), "evict never started");

                        Activity sweep = capture.FindActivities(RedishTelemetry.SpanExpirationSweep).FirstOrDefault(a => a.Status == ActivityStatusCode.Error);
                        Check.NotNull(sweep, "failed sweep is traced");
                        Check.Equal("InvalidOperationException", sweep.GetTagItem(RedishTelemetry.AttributeErrorType) as string);
                        storage.FailKeyEnumeration = false;
                    }
                }),

                Cases.Async(SuiteId, "no-listener", "With no listener attached, commands and sweeps run and nothing throws", async ct =>
                {
                    using (ServerFixture server = await ServerFixture.StartAsync())
                    using (RespRawClient client = await server.ConnectAsync())
                    using (SweepableStorage storage = new SweepableStorage())
                    {
                        Check.Equal("+PONG\r\n", await ServerFixture.SendCommandAsync(client, "PING"));
                        storage.FailKeyEnumeration = true;
                        await storage.RunSweepAsync();
                        storage.FailKeyEnumeration = false;
                    }
                }),

                Cases.Sync(SuiteId, "settings-defaults", "Telemetry settings default to loopback 127.0.0.1 and validate ranges", () =>
                {
                    TelemetrySettings t = new TelemetrySettings();
                    Check.True(t.Enabled);
                    Check.Equal("redish-server", t.ServiceName);
                    Check.Equal("http://127.0.0.1:4317", t.OtlpEndpoint);
                    Check.Equal("127.0.0.1", t.PrometheusHostname);
                    Check.Equal(9464, t.PrometheusPort);
                    Check.False(t.LokiEnabled);
                    Check.Equal(2, t.LogMinimumSeverity);
                    Check.Throws<ArgumentOutOfRangeException>(() => t.LogMinimumSeverity = 8);
                    Check.Throws<ArgumentOutOfRangeException>(() => t.PrometheusPort = 0);
                    t.TraceSamplingRatio = 5;
                    Check.Equal(1.0, t.TraceSamplingRatio);
                    t.ServiceName = "";
                    Check.Equal("redish-server", t.ServiceName);

                    ServerSettings s = new ServerSettings();
                    s.Telemetry = null;
                    Check.NotNull(s.Telemetry, "null restores defaults");
                }),

                Cases.Sync(SuiteId, "radiant-settings-mapping", "Radiant settings subscribe to every source and map protocol, Prometheus, and Loki options", () =>
                {
                    TelemetrySettings t = new TelemetrySettings
                    {
                        OtlpProtocol = "http",
                        OtlpEndpoint = "http://127.0.0.1:4318",
                        PrometheusHostname = "redish",
                        PrometheusPort = 9999,
                        LokiEnabled = true,
                        LokiEndpoint = "http://loki:3100/otlp"
                    };
                    RadiantSettings r = RedishTelemetryHost.BuildRadiantSettings(t, null);
                    Check.Contains(RedishTelemetry.MeterName, r.Sources.MeterNames);
                    Check.Contains(RespTelemetry.MeterName, r.Sources.MeterNames);
                    Check.Contains(RedishTelemetry.ActivitySourceName, r.Sources.ActivitySourceNames);
                    Check.Contains(RespTelemetry.ActivitySourceName, r.Sources.ActivitySourceNames);
                    Check.Equal(OtlpProtocolEnum.HttpProtobuf, r.Otlp.Protocol);
                    Check.True(r.Prometheus.Enable);
                    Check.Equal("redish", r.Prometheus.Hostname);
                    Check.Equal(9999, r.Prometheus.Port);
                    Check.True(r.Loki.Enable);
                    Check.Equal(LabelPolicyEnum.Lenient, r.Metrics.LabelPolicy);
                    Check.True(r.Metrics.IncludeRuntime);
                }),

                Cases.Sync(SuiteId, "host-disabled", "A disabled or null configuration yields an inactive host and never throws", () =>
                {
                    using (RedishTelemetryHost host = RedishTelemetryHost.Start(new TelemetrySettings { Enabled = false }, null))
                    {
                        Check.False(host.IsActive);
                        Check.Null(host.ScrapeUrl);
                    }
                    using (RedishTelemetryHost host = RedishTelemetryHost.Start(null, null))
                    {
                        Check.False(host.IsActive);
                    }
                }),

                Cases.Async(SuiteId, "host-prometheus-scrape", "The Radiant host serves Redish, library, and runtime metrics on the scrape endpoint", async ct =>
                {
                    int port = PortAllocator.GetFreePort();
                    TelemetrySettings settings = new TelemetrySettings
                    {
                        OtlpEnabled = false,
                        EnableLogs = false,
                        PrometheusPort = port
                    };

                    using (RedishTelemetryHost host = RedishTelemetryHost.Start(settings, null))
                    {
                        Check.True(host.IsActive, "host should start");
                        Check.NotNull(host.ScrapeUrl);

                        using (ServerFixture server = await ServerFixture.StartAsync())
                        using (RespRawClient client = await server.ConnectAsync())
                        {
                            await ServerFixture.SendCommandAsync(client, "SET", "scrape", "1");
                        }

                        using (HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                        {
                            string body = await http.GetStringAsync(host.ScrapeUrl);
                            Check.StringContains("redish_commands_total", body);
                            Check.StringContains("redish_command_duration_seconds_bucket", body);
                            Check.StringContains("redisresp_messages_received_total", body);
                            Check.StringContains("process_runtime_dotnet_gc_collections_count_total", body);
                        }
                    }

                    // Dispose releases the port: a second host can bind it.
                    using (RedishTelemetryHost again = RedishTelemetryHost.Start(settings, null))
                    {
                        Check.True(again.IsActive, "port should be released on dispose");
                    }
                }),

                Cases.Sync(SuiteId, "host-port-conflict", "A scrape port already in use does not stop the server from starting", () =>
                {
                    TcpListener blocker = new TcpListener(IPAddress.Loopback, 0);
                    blocker.Start();
                    try
                    {
                        int port = ((IPEndPoint)blocker.LocalEndpoint).Port;
                        TelemetrySettings settings = new TelemetrySettings { OtlpEnabled = false, EnableLogs = false, PrometheusPort = port };
                        RedishTelemetryHost host = null;
                        try
                        {
                            host = RedishTelemetryHost.Start(settings, null);
                            Check.NotNull(host, "Start never returns null");
                        }
                        finally
                        {
                            host?.Dispose();
                        }
                    }
                    finally
                    {
                        blocker.Stop();
                    }
                }),

                Cases.Async(SuiteId, "log-bridge-loki", "Server log lines at Information and above reach the Loki OTLP endpoint; Debug lines do not", async ct =>
                {
                    int lokiPort = PortAllocator.GetFreePort();
                    using (HttpListener loki = new HttpListener())
                    {
                        loki.Prefixes.Add("http://127.0.0.1:" + lokiPort + "/");
                        loki.Start();

                        List<string> paths = new List<string>();
                        StringBuilder bodies = new StringBuilder();
                        Task server = Task.Run(async () =>
                        {
                            while (loki.IsListening)
                            {
                                HttpListenerContext ctx;
                                try { ctx = await loki.GetContextAsync(); } catch { return; }
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    await ctx.Request.InputStream.CopyToAsync(ms);
                                    lock (paths)
                                    {
                                        paths.Add(ctx.Request.Url.AbsolutePath);
                                        bodies.Append(Encoding.UTF8.GetString(ms.ToArray()));
                                    }
                                }
                                ctx.Response.StatusCode = 200;
                                ctx.Response.Close();
                            }
                        });

                        TelemetrySettings settings = new TelemetrySettings
                        {
                            OtlpEnabled = false,
                            EnableMetrics = false,
                            EnableTraces = false,
                            LokiEnabled = true,
                            LokiEndpoint = "http://127.0.0.1:" + lokiPort + "/otlp"
                        };

                        using (LoggingModule logging = new LoggingModule("127.0.0.1", 514, false))
                        {
                            using (RedishTelemetryHost host = RedishTelemetryHost.Start(settings, logging))
                            {
                                Check.True(host.IsActive, "host should start");
                                logging.Info("bridge-info-line");
                                logging.Debug("bridge-debug-line");
                            }

                            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                            while (DateTime.UtcNow < deadline)
                            {
                                lock (paths) { if (bodies.ToString().Contains("bridge-info-line")) break; }
                                await Task.Delay(50);
                            }
                        }

                        loki.Stop();
                        lock (paths)
                        {
                            Check.True(paths.Any(p => p == "/otlp/v1/logs"), "logs should be posted to /otlp/v1/logs");
                            Check.StringContains("bridge-info-line", bodies.ToString());
                            Check.False(bodies.ToString().Contains("bridge-debug-line"), "debug lines must not be exported at the default severity");
                        }
                    }
                }),
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Redish server telemetry",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
