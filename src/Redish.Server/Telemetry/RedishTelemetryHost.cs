namespace Redish.Server.Telemetry
{
    using System;
    using Microsoft.Extensions.Logging;
    using Radiant;
    using Redish.Server.Settings;
    using RedisResp;
    using SyslogLogging;

    /// <summary>
    /// Owns the process's single Radiant telemetry host: subscribes to every meter and activity source in the
    /// process (<c>Redish.Server</c> and the <c>RedisRespServer</c> library), exports over OTLP, serves the
    /// Prometheus scrape endpoint, collects .NET runtime metrics, and forwards server log messages into the
    /// OpenTelemetry logs pipeline (and Loki when enabled) with trace correlation.
    /// </summary>
    /// <remarks>
    /// Best-effort by design: <see cref="Start"/> never throws. If the pipeline cannot be built (for example the
    /// Prometheus port is already bound) a warning is logged and the server runs without exported telemetry.
    /// Dispose once at shutdown to flush exporters and release the scrape port. Not thread safe for concurrent
    /// <see cref="Start"/> and <see cref="Dispose"/>.
    /// </remarks>
    public sealed class RedishTelemetryHost : IDisposable
    {

        #region Public-Members

        /// <summary>
        /// Whether a live Radiant pipeline is running. False when telemetry is disabled or failed to start.
        /// </summary>
        public bool IsActive => _Host != null && _Host.IsEnabled;

        /// <summary>
        /// The Prometheus scrape URL when the scrape endpoint is running; otherwise null.
        /// </summary>
        public string? ScrapeUrl { get; private set; }

        #endregion


        #region Private-Members

        private readonly RadiantHost? _Host;
        private readonly LoggingModule? _Logging;
        private readonly ILogger? _Logger;
        private bool _Disposed;

        #endregion


        #region Constructors-and-Factories

        private RedishTelemetryHost(RadiantHost? host, LoggingModule? logging, ILogger? logger, string? scrapeUrl)
        {
            _Host = host;
            _Logging = logging;
            _Logger = logger;
            ScrapeUrl = scrapeUrl;

            if (_Logging != null && _Logger != null)
            {
                _Logging.MessageLogged += OnMessageLogged;
            }
        }

        /// <summary>
        /// Build and start the telemetry pipeline described by <paramref name="settings"/>.
        /// </summary>
        /// <param name="settings">Telemetry settings. Null is treated as disabled.</param>
        /// <param name="logging">Optional server logging module. Its messages are forwarded to the logs pipeline when
        /// <see cref="TelemetrySettings.EnableLogs"/> is true, and startup warnings are written to it.</param>
        /// <returns>A host; never null. Check <see cref="IsActive"/> to see whether the pipeline is running.</returns>
        public static RedishTelemetryHost Start(TelemetrySettings? settings, LoggingModule? logging)
        {
            if (settings == null || !settings.Enabled)
            {
                return new RedishTelemetryHost(null, null, null, null);
            }

            try
            {
                RadiantSettings radiant = BuildRadiantSettings(settings, logging);
                RadiantHost host = RadiantHost.Start(radiant);

                ILogger? logger = settings.EnableLogs ? host.CreateLogger("Redish.Server") : null;
                string? scrapeUrl = settings.EnableMetrics && settings.PrometheusEnabled ? radiant.Prometheus.ToScrapeUrl() : null;
                return new RedishTelemetryHost(host, logging, logger, scrapeUrl);
            }
            catch (Exception ex)
            {
                TryWarn(logging, "[Telemetry] telemetry pipeline failed to start; continuing without exported telemetry: " + DescribeException(ex));
                return new RedishTelemetryHost(null, null, null, null);
            }
        }

        #endregion


        #region Public-Methods

        /// <summary>
        /// Build the Radiant settings for the given telemetry settings. Exposed so the mapping can be verified.
        /// </summary>
        /// <param name="settings">Telemetry settings. Must be non-null.</param>
        /// <param name="logging">Optional logging module that receives Radiant diagnostic messages.</param>
        /// <returns>The Radiant settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public static RadiantSettings BuildRadiantSettings(TelemetrySettings settings, LoggingModule? logging)
        {
            ArgumentNullException.ThrowIfNull(settings);

            RadiantSettings radiant = new RadiantSettings(settings.ServiceName);
            radiant.Enable = settings.Enabled;

            radiant.Sources.AddMeter(RedishTelemetry.MeterName);
            radiant.Sources.AddActivitySource(RedishTelemetry.ActivitySourceName);
            radiant.Sources.AddMeter(RespTelemetry.MeterName);
            radiant.Sources.AddActivitySource(RespTelemetry.ActivitySourceName);

            radiant.Metrics.Enable = settings.EnableMetrics;
            radiant.Metrics.IncludeRuntime = true;
            radiant.Metrics.IncludeProcess = true;
            radiant.Metrics.LabelPolicy = LabelPolicyEnum.Lenient;

            radiant.Traces.Enable = settings.EnableTraces;
            radiant.Traces.SamplingRatio = settings.TraceSamplingRatio;
            radiant.Traces.PropagateContext = true;

            radiant.Logs.Enable = settings.EnableLogs;
            radiant.Logs.MinimumSeverity = settings.LogMinimumSeverity;

            radiant.Otlp.Enable = settings.OtlpEnabled;
            radiant.Otlp.Endpoint = settings.OtlpEndpoint;
            radiant.Otlp.Protocol = IsHttpProtocol(settings.OtlpProtocol) ? OtlpProtocolEnum.HttpProtobuf : OtlpProtocolEnum.Grpc;

            radiant.Prometheus.Enable = settings.EnableMetrics && settings.PrometheusEnabled;
            radiant.Prometheus.Hostname = settings.PrometheusHostname;
            radiant.Prometheus.Port = settings.PrometheusPort;

            radiant.Loki.Enable = settings.EnableLogs && settings.LokiEnabled;
            radiant.Loki.Endpoint = settings.LokiEndpoint;
            radiant.Loki.MinimumSeverity = settings.LogMinimumSeverity;

            radiant.DiagnosticCallback = message => TryWarn(logging, "[Telemetry] " + message);
            return radiant;
        }

        /// <summary>
        /// Flush exporters, detach the log bridge, and release the scrape port. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try
            {
                if (_Logging != null && _Logger != null) _Logging.MessageLogged -= OnMessageLogged;
            }
            catch { }

            try { _Host?.Dispose(); } catch { }
        }

        #endregion


        #region Private-Methods

        private static bool IsHttpProtocol(string protocol)
        {
            return protocol.Equals("http", StringComparison.OrdinalIgnoreCase)
                || protocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase)
                || protocol.Equals("httpprotobuf", StringComparison.OrdinalIgnoreCase);
        }

        private void OnMessageLogged(LogEntry entry)
        {
            try
            {
                if (_Logger == null || entry == null) return;

                LogLevel level = ToLogLevel(entry.Severity);
                if (!_Logger.IsEnabled(level)) return;

                _Logger.Log(level, entry.Exception, "{Message}", entry.Message);
            }
            catch
            {
                // Log forwarding is best-effort and must never affect the caller.
            }
        }

        private static LogLevel ToLogLevel(Severity severity)
        {
            switch (severity)
            {
                case Severity.Debug: return LogLevel.Debug;
                case Severity.Info: return LogLevel.Information;
                case Severity.Warn: return LogLevel.Warning;
                case Severity.Error: return LogLevel.Error;
                default: return LogLevel.Critical;
            }
        }

        private static string DescribeException(Exception ex)
        {
            // The root cause (for example a bound port) is usually an inner exception.
            string description = ex.Message;
            Exception? inner = ex.InnerException;
            int depth = 0;
            while (inner != null && depth < 5)
            {
                description += " -> " + inner.GetType().Name + ": " + inner.Message;
                inner = inner.InnerException;
                depth++;
            }
            return description;
        }

        private static void TryWarn(LoggingModule? logging, string message)
        {
            try { logging?.Warn(message); } catch { }
        }

        #endregion

    }
}
