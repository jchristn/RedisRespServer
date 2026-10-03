namespace Redish.Server.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading;
    using RedisResp;

    /// <summary>
    /// Holder for the Redish server's meter, activity source, instruments, and recording helpers.
    /// </summary>
    /// <remarks>
    /// Every method is best-effort and never throws into the caller. With no listener attached each call is an
    /// enabled check and a return. Names come from <see cref="RedishTelemetry"/>. Thread safe.
    /// </remarks>
    internal static class RedishInstrumentation
    {

        #region Public-Members

        internal static readonly ActivitySource ActivitySource = new ActivitySource(RedishTelemetry.ActivitySourceName, Constants.Version);

        internal static readonly Meter Meter = new Meter(RedishTelemetry.MeterName, Constants.Version);

        #endregion


        #region Private-Members

        private static readonly HashSet<string> _KnownOperations = new HashSet<string>(StringComparer.Ordinal)
        {
            "PING", "ECHO", "GET", "SET", "DEL", "EXISTS", "KEYS", "FLUSHDB", "AUTH", "INFO", "CLIENT", "CONFIG",
            "SENTINEL", "CLUSTER", "SUBSCRIBE", "UNSUBSCRIBE", "PUBLISH", "HELLO", "COMMAND", "SELECT", "ROLE",
            "TIME", "MEMORY", "ACL", "MODULE", "LATENCY", "SCAN", "TYPE", "TTL", "MGET", "MSET", "INCR", "INCRBY",
            "DECR", "HMSET", "HGETALL", "INCRBYFLOAT", "STRLEN", "GETRANGE", "HSET", "HGET", "HDEL", "HLEN",
            "DBSIZE", "EXPIRE", "PERSIST", "HSCAN", "HEXISTS", "RPUSH", "LPUSH", "RPOP", "LPOP", "LRANGE", "LLEN",
            "SADD", "SREM", "SMEMBERS", "SISMEMBER", "SCARD", "SPOP", "SRANDMEMBER", "ZADD", "ZREM", "ZSCORE",
            "ZCARD", "ZRANGE", "ZINCRBY", "JSON.SET", "JSON.GET", "JSON.DEL", "XADD", "XRANGE", "XLEN", "XDEL", "XINFO"
        };

        private static readonly HashSet<string> _LookupOperations = new HashSet<string>(StringComparer.Ordinal)
        {
            "GET", "HGET", "JSON.GET", "ZSCORE"
        };

        private static readonly HashSet<string> _KnownErrorCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "ERR", "WRONGTYPE", "WRONGPASS", "NOAUTH", "NOPERM", "EXECABORT", "BUSY", "NOSCRIPT", "READONLY"
        };

        private static readonly double[] _FastSecondsBuckets = new double[]
        {
            0.00005, 0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5
        };

        private static readonly long[] _SizeBuckets = new long[]
        {
            16, 64, 256, 1024, 4096, 16384, 65536, 262144, 1048576, 4194304, 16777216
        };

        private static readonly object _ServersLock = new object();
        private static readonly List<ServerTelemetrySource> _Servers = new List<ServerTelemetrySource>();
        private static long _LastSweepSuccessUnixSeconds = 0;

        private static readonly Counter<long> _Commands = Meter.CreateCounter<long>(
            RedishTelemetry.Commands, "{command}", "Commands processed, by operation and outcome.");

        private static readonly Histogram<double> _CommandDuration = Meter.CreateHistogram<double>(
            RedishTelemetry.CommandDuration, "s", "End-to-end command latency (execute plus respond), by operation and outcome.", null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = _FastSecondsBuckets });

        private static readonly Histogram<double> _CommandStageDuration = Meter.CreateHistogram<double>(
            RedishTelemetry.CommandStageDuration, "s", "Per-stage command latency, by stage and operation.", null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = _FastSecondsBuckets });

        private static readonly Counter<long> _CommandErrors = Meter.CreateCounter<long>(
            RedishTelemetry.CommandErrors, "{error}", "Failed commands, by operation and error type.");

        private static readonly Histogram<long> _ResponseSize = Meter.CreateHistogram<long>(
            RedishTelemetry.ResponseSize, "By", "Reply size written to clients, by operation.", null,
            new InstrumentAdvice<long> { HistogramBucketBoundaries = _SizeBuckets });

        private static readonly Counter<long> _ResponseErrors = Meter.CreateCounter<long>(
            RedishTelemetry.ResponseErrors, "{error}", "Replies that could not be delivered, by error type.");

        private static readonly Counter<long> _KeyspaceLookups = Meter.CreateCounter<long>(
            RedishTelemetry.KeyspaceLookups, "{lookup}", "Single-key lookups, by operation and hit or miss.");

        private static readonly Counter<long> _AuthAttempts = Meter.CreateCounter<long>(
            RedishTelemetry.AuthAttempts, "{attempt}", "AUTH attempts, by outcome.");

        private static readonly Counter<long> _ExpirationSweeps = Meter.CreateCounter<long>(
            RedishTelemetry.ExpirationSweeps, "{sweep}", "Expiration sweep jobs, by outcome.");

        private static readonly Histogram<double> _ExpirationSweepDuration = Meter.CreateHistogram<double>(
            RedishTelemetry.ExpirationSweepDuration, "s", "Expiration sweep duration including queue wait, by outcome.", null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = _FastSecondsBuckets });

        private static readonly Histogram<double> _ExpirationStageDuration = Meter.CreateHistogram<double>(
            RedishTelemetry.ExpirationStageDuration, "s", "Expiration sweep stage duration, by stage and outcome.", null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = _FastSecondsBuckets });

        private static readonly Counter<long> _ExpirationStageEvents = Meter.CreateCounter<long>(
            RedishTelemetry.ExpirationStageEvents, "{stage}", "Expiration sweep stage executions, by stage and outcome.");

        private static readonly UpDownCounter<long> _ExpirationSweepsActive = Meter.CreateUpDownCounter<long>(
            RedishTelemetry.ExpirationSweepsActive, "{sweep}", "Expiration sweeps running right now.");

        private static readonly Counter<long> _ExpirationKeysScanned = Meter.CreateCounter<long>(
            RedishTelemetry.ExpirationKeysScanned, "{key}", "Keys examined by expiration sweeps.");

        private static readonly Counter<long> _ExpirationKeysExpired = Meter.CreateCounter<long>(
            RedishTelemetry.ExpirationKeysExpired, "{key}", "Keys removed because their TTL elapsed, by trigger.");

        private static readonly ObservableGauge<long> _ClientsGauge = Meter.CreateObservableGauge<long>(
            RedishTelemetry.Clients, ObserveClients, "{client}", "Clients tracked by the server, by RESP protocol.");

        private static readonly ObservableGauge<long> _StorageKeysGauge = Meter.CreateObservableGauge<long>(
            RedishTelemetry.StorageKeys, ObserveStorageKeys, "{key}", "Keys held in storage, including expired keys not yet swept.");

        private static readonly ObservableGauge<long> _LastSuccessGauge = Meter.CreateObservableGauge<long>(
            RedishTelemetry.ExpirationLastSuccess, ObserveLastSweepSuccess, "s", "Unix time of the last successful expiration sweep.");

        private static readonly ObservableGauge<long> _BuildInfoGauge = Meter.CreateObservableGauge<long>(
            RedishTelemetry.BuildInfo, ObserveBuildInfo, "{info}", "Build and safe configuration facts carried as labels; value is 1.");

        private static readonly ObservableGauge<long> _DatabasesGauge = Meter.CreateObservableGauge<long>(
            RedishTelemetry.ConfigDatabases, ObserveDatabases, "{database}", "Configured logical database count.");

        private static readonly ObservableGauge<double> _UptimeGauge = Meter.CreateObservableGauge<double>(
            RedishTelemetry.Uptime, ObserveUptime, "s", "Seconds since the server started.");

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        internal static void RegisterServer(ServerTelemetrySource source)
        {
            if (source == null) return;
            lock (_ServersLock)
            {
                if (!_Servers.Contains(source)) _Servers.Add(source);
            }
        }

        internal static void UnregisterServer(ServerTelemetrySource source)
        {
            if (source == null) return;
            lock (_ServersLock)
            {
                _Servers.Remove(source);
            }
        }

        internal static string NormalizeOperation(string upperCommand)
        {
            if (string.IsNullOrEmpty(upperCommand)) return RedishTelemetry.OperationUnknown;
            return _KnownOperations.Contains(upperCommand) ? upperCommand : RedishTelemetry.OperationUnknown;
        }

        internal static Activity? StartCommand(string operation, Guid clientGuid, int argumentCount)
        {
            try
            {
                Activity? activity = ActivitySource.StartActivity(RedishTelemetry.SpanCommand + " " + operation, ActivityKind.Internal);
                if (activity != null)
                {
                    activity.SetTag(RedishTelemetry.AttributeDbSystem, "redis");
                    activity.SetTag(RedishTelemetry.AttributeOperation, operation);
                    activity.SetTag(RedishTelemetry.AttributeArgumentCount, argumentCount);
                    activity.SetTag(RedishTelemetry.AttributeClientId, clientGuid.ToString());
                }
                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static Activity? StartStage(string stage)
        {
            try
            {
                return ActivitySource.StartActivity(RedishTelemetry.SpanStagePrefix + stage, ActivityKind.Internal);
            }
            catch
            {
                return null;
            }
        }

        internal static void StageCompleted(Activity? activity, string stage, string operation, long startTimestamp, Exception? ex)
        {
            try
            {
                _CommandStageDuration.Record(ElapsedSeconds(startTimestamp), new TagList
                {
                    { RedishTelemetry.AttributeStage, stage },
                    { RedishTelemetry.AttributeOperation, operation }
                });

                if (activity != null)
                {
                    if (ex != null) RecordException(activity, ex);
                    else activity.SetStatus(ActivityStatusCode.Ok);
                }
            }
            catch { }
            finally
            {
                activity?.Dispose();
            }
        }

        internal static void CommandCompleted(
            Activity? activity,
            string operation,
            long startTimestamp,
            string outcome,
            string? errorType,
            int responseBytes,
            Exception? ex)
        {
            try
            {
                TagList tags = new TagList
                {
                    { RedishTelemetry.AttributeOperation, operation },
                    { RedishTelemetry.AttributeOutcome, outcome }
                };
                _Commands.Add(1, tags);
                _CommandDuration.Record(ElapsedSeconds(startTimestamp), tags);

                if (responseBytes > 0)
                {
                    _ResponseSize.Record(responseBytes, new KeyValuePair<string, object?>(RedishTelemetry.AttributeOperation, operation));
                }

                if (errorType != null)
                {
                    _CommandErrors.Add(1, new TagList
                    {
                        { RedishTelemetry.AttributeOperation, operation },
                        { RedishTelemetry.AttributeErrorType, errorType }
                    });
                }

                if (activity != null)
                {
                    activity.SetTag(RedishTelemetry.AttributeOutcome, outcome);
                    activity.SetTag(RedishTelemetry.AttributeResponseSize, responseBytes);

                    if (ex != null)
                    {
                        RecordException(activity, ex);
                    }
                    else if (errorType != null)
                    {
                        activity.SetTag(RedishTelemetry.AttributeErrorType, errorType);
                        activity.SetStatus(ActivityStatusCode.Error, outcome + ": " + errorType);
                    }
                    else
                    {
                        activity.SetStatus(ActivityStatusCode.Ok);
                    }
                }
            }
            catch { }
            finally
            {
                activity?.Dispose();
            }
        }

        internal static void RecordLookup(string operation, string response)
        {
            try
            {
                if (!_LookupOperations.Contains(operation) || string.IsNullOrEmpty(response) || response[0] == '-') return;

                bool miss = response.StartsWith("$-1", StringComparison.Ordinal)
                    || response.StartsWith("_\r\n", StringComparison.Ordinal)
                    || response.StartsWith("*-1", StringComparison.Ordinal);

                _KeyspaceLookups.Add(1, new TagList
                {
                    { RedishTelemetry.AttributeOperation, operation },
                    { RedishTelemetry.AttributeLookupResult, miss ? RedishTelemetry.LookupMiss : RedishTelemetry.LookupHit }
                });
            }
            catch { }
        }

        internal static string? ErrorReplyCode(string response)
        {
            if (string.IsNullOrEmpty(response) || response[0] != '-') return null;

            int end = 1;
            while (end < response.Length && response[end] != ' ' && response[end] != '\r') end++;
            string code = response.Substring(1, end - 1);
            return _KnownErrorCodes.Contains(code) ? code : "OTHER";
        }

        internal static void ResponseFailed(string errorType)
        {
            try { _ResponseErrors.Add(1, new KeyValuePair<string, object?>(RedishTelemetry.AttributeErrorType, errorType)); } catch { }
        }

        internal static void AuthAttempt(string outcome)
        {
            try { _AuthAttempts.Add(1, new KeyValuePair<string, object?>(RedishTelemetry.AttributeOutcome, outcome)); } catch { }
        }

        internal static void KeysExpired(long count, string trigger)
        {
            if (count <= 0) return;
            try { _ExpirationKeysExpired.Add(count, new KeyValuePair<string, object?>(RedishTelemetry.AttributeTrigger, trigger)); } catch { }
        }

        internal static void SweepStarted()
        {
            try { _ExpirationSweepsActive.Add(1); } catch { }
        }

        internal static void SweepCompleted(
            long firedTimestamp,
            long runStartTimestamp,
            long snapshotEndTimestamp,
            long endTimestamp,
            long keysScanned,
            long keysExpired,
            Exception? ex)
        {
            try
            {
                _ExpirationSweepsActive.Add(-1);

                bool failed = ex != null;
                string outcome = failed ? RedishTelemetry.OutcomeFailure : RedishTelemetry.OutcomeSuccess;
                bool snapshotDone = snapshotEndTimestamp > 0;

                // A failure during the snapshot stage leaves the evict stage unstarted.
                string snapshotOutcome = failed && !snapshotDone ? RedishTelemetry.OutcomeFailure : RedishTelemetry.OutcomeSuccess;
                long snapshotEnd = snapshotDone ? snapshotEndTimestamp : endTimestamp;

                RecordSweepStage(RedishTelemetry.StageQueued, firedTimestamp, runStartTimestamp, RedishTelemetry.OutcomeSuccess);
                RecordSweepStage(RedishTelemetry.StageSnapshot, runStartTimestamp, snapshotEnd, snapshotOutcome);
                if (snapshotDone) RecordSweepStage(RedishTelemetry.StageEvict, snapshotEndTimestamp, endTimestamp, outcome);

                KeyValuePair<string, object?> outcomeTag = new KeyValuePair<string, object?>(RedishTelemetry.AttributeOutcome, outcome);
                _ExpirationSweeps.Add(1, outcomeTag);
                _ExpirationSweepDuration.Record(Seconds(firedTimestamp, endTimestamp), outcomeTag);
                if (keysScanned > 0) _ExpirationKeysScanned.Add(keysScanned);
                KeysExpired(keysExpired, RedishTelemetry.TriggerActive);

                if (!failed)
                {
                    Interlocked.Exchange(ref _LastSweepSuccessUnixSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                }

                // Idle sweeps run every second; only sweeps that did work or failed are worth a trace.
                if (failed || keysExpired > 0)
                {
                    EmitSweepTrace(firedTimestamp, runStartTimestamp, snapshotEndTimestamp, endTimestamp, keysScanned, keysExpired, ex);
                }
            }
            catch { }
        }

        internal static void RecordException(Activity activity, Exception ex)
        {
            try
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.SetTag(RedishTelemetry.AttributeErrorType, ex.GetType().Name);
                activity.AddEvent(new ActivityEvent("exception", default, new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message },
                    { "exception.stacktrace", ex.ToString() }
                }));
            }
            catch { }
        }

        internal static double ElapsedSeconds(long startTimestamp)
        {
            return Seconds(startTimestamp, Stopwatch.GetTimestamp());
        }

        #endregion


        #region Private-Methods

        private static double Seconds(long startTimestamp, long endTimestamp)
        {
            long delta = endTimestamp - startTimestamp;
            return delta <= 0 ? 0 : delta / (double)Stopwatch.Frequency;
        }

        private static void RecordSweepStage(string stage, long start, long end, string outcome)
        {
            TagList tags = new TagList
            {
                { RedishTelemetry.AttributeStage, stage },
                { RedishTelemetry.AttributeOutcome, outcome }
            };
            _ExpirationStageDuration.Record(Seconds(start, end), tags);
            _ExpirationStageEvents.Add(1, tags);
        }

        private static void EmitSweepTrace(
            long firedTimestamp,
            long runStartTimestamp,
            long snapshotEndTimestamp,
            long endTimestamp,
            long keysScanned,
            long keysExpired,
            Exception? ex)
        {
            if (!ActivitySource.HasListeners()) return;

            // Spans are built after the fact from recorded timestamps, so the decision to keep the trace can
            // depend on the result. The sweep is a background job: it is always a root, never a child.
            DateTimeOffset now = DateTimeOffset.UtcNow;
            long nowTs = Stopwatch.GetTimestamp();
            Func<long, DateTimeOffset> wall = ts => now - TimeSpan.FromSeconds(Seconds(ts, nowTs));

            Activity? previous = Activity.Current;
            Activity.Current = null;
            try
            {
                using (Activity? root = ActivitySource.StartActivity(
                    RedishTelemetry.SpanExpirationSweep, ActivityKind.Internal, default(ActivityContext), null, null, wall(firedTimestamp)))
                {
                    if (root == null) return;

                    root.SetTag(RedishTelemetry.AttributeKeysScanned, keysScanned);
                    root.SetTag(RedishTelemetry.AttributeKeysExpired, keysExpired);

                    EmitStageSpan(root, RedishTelemetry.StageQueued, wall(firedTimestamp), wall(runStartTimestamp), null);
                    if (snapshotEndTimestamp > 0)
                    {
                        EmitStageSpan(root, RedishTelemetry.StageSnapshot, wall(runStartTimestamp), wall(snapshotEndTimestamp), null);
                        EmitStageSpan(root, RedishTelemetry.StageEvict, wall(snapshotEndTimestamp), wall(endTimestamp), ex);
                    }
                    else
                    {
                        EmitStageSpan(root, RedishTelemetry.StageSnapshot, wall(runStartTimestamp), wall(endTimestamp), ex);
                    }

                    if (ex != null) RecordException(root, ex);
                    else root.SetStatus(ActivityStatusCode.Ok);

                    root.SetEndTime(wall(endTimestamp).UtcDateTime);
                }
            }
            finally
            {
                Activity.Current = previous;
            }
        }

        private static void EmitStageSpan(Activity root, string stage, DateTimeOffset start, DateTimeOffset end, Exception? ex)
        {
            using (Activity? span = ActivitySource.StartActivity(
                RedishTelemetry.SpanStagePrefix + stage, ActivityKind.Internal, root.Context, null, null, start))
            {
                if (span == null) return;
                if (ex != null) RecordException(span, ex);
                else span.SetStatus(ActivityStatusCode.Ok);
                span.SetEndTime(end.UtcDateTime);
            }
        }

        private static List<ServerTelemetrySource> SnapshotServers()
        {
            lock (_ServersLock)
            {
                return _Servers.ToList();
            }
        }

        private static IEnumerable<Measurement<long>> ObserveClients()
        {
            long resp2 = 0;
            long resp3 = 0;
            foreach (ServerTelemetrySource server in SnapshotServers())
            {
                try
                {
                    foreach (RespVersionEnum version in server.ClientProtocols())
                    {
                        if (version == RespVersionEnum.RESP3) resp3++;
                        else resp2++;
                    }
                }
                catch { }
            }

            return new[]
            {
                new Measurement<long>(resp2, new KeyValuePair<string, object?>(RedishTelemetry.AttributeRespProtocol, "RESP2")),
                new Measurement<long>(resp3, new KeyValuePair<string, object?>(RedishTelemetry.AttributeRespProtocol, "RESP3"))
            };
        }

        private static IEnumerable<Measurement<long>> ObserveStorageKeys()
        {
            List<ServerTelemetrySource> servers = SnapshotServers();
            if (servers.Count == 0) return Array.Empty<Measurement<long>>();

            long total = 0;
            foreach (ServerTelemetrySource server in servers)
            {
                try { total += server.StorageKeyCount(); } catch { }
            }
            return new[] { new Measurement<long>(total) };
        }

        private static IEnumerable<Measurement<long>> ObserveLastSweepSuccess()
        {
            return new[] { new Measurement<long>(Interlocked.Read(ref _LastSweepSuccessUnixSeconds)) };
        }

        private static IEnumerable<Measurement<long>> ObserveBuildInfo()
        {
            Dictionary<string, Measurement<long>> distinct = new Dictionary<string, Measurement<long>>();
            foreach (ServerTelemetrySource server in SnapshotServers())
            {
                string key = server.StorageMode + "|" + server.RedisVersion;
                if (distinct.ContainsKey(key)) continue;

                distinct[key] = new Measurement<long>(1, new TagList
                {
                    { RedishTelemetry.AttributeServiceVersion, Constants.Version },
                    { RedishTelemetry.AttributeStorageMode, server.StorageMode },
                    { RedishTelemetry.AttributeRedisVersion, server.RedisVersion }
                });
            }
            return distinct.Values;
        }

        private static IEnumerable<Measurement<long>> ObserveDatabases()
        {
            List<ServerTelemetrySource> servers = SnapshotServers();
            if (servers.Count == 0) return Array.Empty<Measurement<long>>();
            return new[] { new Measurement<long>(servers.Max(s => (long)s.DatabaseCount)) };
        }

        private static IEnumerable<Measurement<double>> ObserveUptime()
        {
            List<ServerTelemetrySource> servers = SnapshotServers();
            if (servers.Count == 0) return Array.Empty<Measurement<double>>();
            DateTime earliest = servers.Min(s => s.StartedUtc);
            return new[] { new Measurement<double>((DateTime.UtcNow - earliest).TotalSeconds) };
        }

        #endregion

    }
}
