namespace RedisResp
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Internal holder for the library's meter, activity source, and instruments.
    /// </summary>
    /// <remarks>
    /// Every recording method is best-effort: it never throws into the caller. When no listener is attached,
    /// each call reduces to an enabled check and returns. Names come from <see cref="RespTelemetry"/>.
    /// This class is thread safe.
    /// </remarks>
    internal static class RespInstrumentation
    {

        #region Public-Members

        internal static readonly ActivitySource ActivitySource = new ActivitySource(RespTelemetry.ActivitySourceName, ResolveVersion());

        internal static readonly Meter Meter = new Meter(RespTelemetry.MeterName, ResolveVersion());

        #endregion


        #region Private-Members

        private static readonly double[] _FastSecondsBuckets = new double[]
        {
            0.00005, 0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5
        };

        private static readonly double[] _ConnectionSecondsBuckets = new double[]
        {
            0.1, 0.5, 1, 5, 15, 30, 60, 300, 900, 1800, 3600, 14400, 43200, 86400
        };

        private static readonly double[] _SizeBuckets = new double[]
        {
            16, 64, 256, 1024, 4096, 16384, 65536, 262144, 1048576, 4194304, 16777216
        };

        private static readonly UpDownCounter<long> _ListenersActive = Meter.CreateUpDownCounter<long>(
            RespTelemetry.ListenersActive, "{listener}", "RESP listeners currently accepting connections.");

        private static readonly UpDownCounter<long> _ConnectionsActive = Meter.CreateUpDownCounter<long>(
            RespTelemetry.ConnectionsActive, "{connection}", "Client connections currently open.");

        private static readonly Counter<long> _ConnectionsAccepted = Meter.CreateCounter<long>(
            RespTelemetry.ConnectionsAccepted, "{connection}", "Client connections accepted.");

        private static readonly Counter<long> _ConnectionsClosed = Meter.CreateCounter<long>(
            RespTelemetry.ConnectionsClosed, "{connection}", "Client connections closed, by close reason.");

        private static readonly Histogram<double> _ConnectionDuration = Meter.CreateHistogram<double>(
            RespTelemetry.ConnectionDuration, "s", "Client connection lifetime, by close reason.", null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = _ConnectionSecondsBuckets });

        private static readonly Counter<long> _AcceptErrors = Meter.CreateCounter<long>(
            RespTelemetry.AcceptErrors, "{error}", "Failures accepting a client connection, by error type.");

        private static readonly Counter<long> _BytesReceived = Meter.CreateCounter<long>(
            RespTelemetry.BytesReceived, "By", "Bytes read from client sockets.");

        private static readonly Counter<long> _MessagesReceived = Meter.CreateCounter<long>(
            RespTelemetry.MessagesReceived, "{message}", "Complete RESP messages parsed, by RESP type and protocol.");

        private static readonly Histogram<long> _MessageSize = Meter.CreateHistogram<long>(
            RespTelemetry.MessageSize, "By", "Size of complete RESP messages, by RESP type.", null,
            new InstrumentAdvice<long> { HistogramBucketBoundaries = Array.ConvertAll(_SizeBuckets, b => (long)b) });

        private static readonly Histogram<double> _DispatchDuration = Meter.CreateHistogram<double>(
            RespTelemetry.DispatchDuration, "s", "Time spent dispatching one RESP message to subscribers, by RESP type and outcome.", null,
            new InstrumentAdvice<double> { HistogramBucketBoundaries = _FastSecondsBuckets });

        private static readonly Counter<long> _ParseErrors = Meter.CreateCounter<long>(
            RespTelemetry.ParseErrors, "{error}", "Input the RESP parser could not interpret, by error type.");

        private static readonly Counter<long> _ClientErrors = Meter.CreateCounter<long>(
            RespTelemetry.ClientErrors, "{error}", "Unexpected errors on a client connection loop, by error type.");

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        internal static void ListenerStarted()
        {
            try { _ListenersActive.Add(1); } catch { }
        }

        internal static void ListenerStopped()
        {
            try { _ListenersActive.Add(-1); } catch { }
        }

        internal static void ConnectionAccepted()
        {
            try
            {
                _ConnectionsAccepted.Add(1);
                _ConnectionsActive.Add(1);
            }
            catch { }
        }

        internal static void ConnectionClosed(string reason, long startTimestamp)
        {
            try
            {
                KeyValuePair<string, object> tag = new KeyValuePair<string, object>(RespTelemetry.AttributeCloseReason, reason);
                _ConnectionsActive.Add(-1);
                _ConnectionsClosed.Add(1, tag);
                _ConnectionDuration.Record(ElapsedSeconds(startTimestamp), tag);
            }
            catch { }
        }

        internal static void AcceptFailed(Exception ex)
        {
            try { _AcceptErrors.Add(1, new KeyValuePair<string, object>(RespTelemetry.AttributeErrorType, ErrorType(ex))); } catch { }
        }

        internal static void ClientFailed(Exception ex)
        {
            try { _ClientErrors.Add(1, new KeyValuePair<string, object>(RespTelemetry.AttributeErrorType, ErrorType(ex))); } catch { }
        }

        internal static void BytesRead(int count)
        {
            try { _BytesReceived.Add(count); } catch { }
        }

        internal static void ParseFailed(string errorType)
        {
            try { _ParseErrors.Add(1, new KeyValuePair<string, object>(RespTelemetry.AttributeErrorType, errorType)); } catch { }
        }

        internal static Activity StartDispatch()
        {
            try
            {
                return ActivitySource.StartActivity(RespTelemetry.SpanDispatch, ActivityKind.Server);
            }
            catch
            {
                return null;
            }
        }

        internal static void DispatchCompleted(
            Activity activity,
            long startTimestamp,
            string respType,
            string respProtocol,
            int messageSize,
            string outcome,
            Exception ex)
        {
            try
            {
                TagList typeTags = new TagList
                {
                    { RespTelemetry.AttributeRespType, respType },
                    { RespTelemetry.AttributeRespProtocol, respProtocol }
                };
                _MessagesReceived.Add(1, typeTags);
                _MessageSize.Record(messageSize, new KeyValuePair<string, object>(RespTelemetry.AttributeRespType, respType));
                _DispatchDuration.Record(ElapsedSeconds(startTimestamp), new TagList
                {
                    { RespTelemetry.AttributeRespType, respType },
                    { RespTelemetry.AttributeOutcome, outcome }
                });

                if (activity != null)
                {
                    activity.DisplayName = RespTelemetry.SpanDispatch + " " + respType;
                    activity.SetTag(RespTelemetry.AttributeRespType, respType);
                    activity.SetTag(RespTelemetry.AttributeRespProtocol, respProtocol);
                    activity.SetTag(RespTelemetry.AttributeMessageSize, messageSize);
                    activity.SetTag(RespTelemetry.AttributeOutcome, outcome);

                    if (ex != null)
                    {
                        RecordException(activity, ex);
                    }
                    else if (outcome == RespTelemetry.OutcomeOk)
                    {
                        activity.SetStatus(ActivityStatusCode.Ok);
                    }
                    else
                    {
                        activity.SetStatus(ActivityStatusCode.Error, outcome);
                        activity.SetTag(RespTelemetry.AttributeErrorType, outcome);
                    }
                }
            }
            catch { }
        }

        internal static void RecordException(Activity activity, Exception ex)
        {
            if (activity == null || ex == null) return;

            try
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.SetTag(RespTelemetry.AttributeErrorType, ErrorType(ex));
                activity.AddEvent(new ActivityEvent("exception", default, new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message },
                    { "exception.stacktrace", ex.ToString() }
                }));
            }
            catch { }
        }

        internal static string ErrorType(Exception ex)
        {
            if (ex == null) return "unknown";
            return ex.GetType().Name;
        }

        internal static double ElapsedSeconds(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
        }

        #endregion


        #region Private-Methods

        private static string ResolveVersion()
        {
            try
            {
                return typeof(RespInstrumentation).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            }
            catch
            {
                return "0.0.0";
            }
        }

        #endregion

    }
}
