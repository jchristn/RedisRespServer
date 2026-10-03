namespace Redish.Server.Settings
{
    using System;

    /// <summary>
    /// Telemetry settings for the Redish server: metrics, traces, and logs exported through Radiant.
    /// </summary>
    /// <remarks>
    /// Modeled on Pneuma's <c>TelemetrySettings</c>. Loopback defaults use <c>127.0.0.1</c> rather than
    /// <c>localhost</c>. Telemetry is best-effort: if the pipeline cannot start (for example the Prometheus
    /// port is taken) the server logs a warning and runs without it. Changes take effect on restart.
    /// This type is not thread safe; configure it before the server starts.
    /// </remarks>
    public class TelemetrySettings
    {

        #region Public-Members

        /// <summary>
        /// Master switch for the telemetry pipeline. Default true. When false, no exporter or scrape
        /// endpoint is started and instrumentation in the server and library stays a no-op.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Service name stamped as the <c>service.name</c> resource attribute. Default <c>redish-server</c>.
        /// Null or empty values reset to the default.
        /// </summary>
        public string ServiceName
        {
            get => _ServiceName;
            set => _ServiceName = string.IsNullOrWhiteSpace(value) ? DefaultServiceName : value;
        }

        /// <summary>
        /// Whether metrics are collected and exported. Default true.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Whether traces are collected and exported. Default true.
        /// </summary>
        public bool EnableTraces { get; set; } = true;

        /// <summary>
        /// Whether server log messages are forwarded into the OpenTelemetry logs pipeline (OTLP and, when
        /// <see cref="LokiEnabled"/> is true, Loki). Default true. Console, file, and syslog logging are unaffected.
        /// </summary>
        public bool EnableLogs { get; set; } = true;

        /// <summary>
        /// Head-based trace sampling ratio. Default 1.0 (sample everything). Minimum 0.0 (sample nothing),
        /// maximum 1.0. Values outside the range are clamped.
        /// </summary>
        public double TraceSamplingRatio
        {
            get => _TraceSamplingRatio;
            set => _TraceSamplingRatio = value < 0 ? 0 : (value > 1 ? 1 : value);
        }

        /// <summary>
        /// Minimum log severity forwarded to the logs pipeline, on Radiant's 0..7 scale
        /// (0 Trace, 1 Debug, 2 Information, 4 Warning, 5 Error, 6 Critical, 7 none). Default 2 (Information),
        /// which keeps per-command debug lines (which can contain key names and values) out of exported logs.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 0..7.</exception>
        public int LogMinimumSeverity
        {
            get => _LogMinimumSeverity;
            set
            {
                if (value < 0 || value > 7) throw new ArgumentOutOfRangeException(nameof(LogMinimumSeverity), "LogMinimumSeverity must be between 0 and 7.");
                _LogMinimumSeverity = value;
            }
        }

        /// <summary>
        /// Whether to push metrics, traces, and logs over OTLP. Default true.
        /// </summary>
        public bool OtlpEnabled { get; set; } = true;

        /// <summary>
        /// OTLP collector or Tempo endpoint. Default <c>http://127.0.0.1:4317</c> (gRPC).
        /// Use port 4318 with <see cref="OtlpProtocol"/> set to <c>http</c>. Null or empty values reset to the default.
        /// </summary>
        public string OtlpEndpoint
        {
            get => _OtlpEndpoint;
            set => _OtlpEndpoint = string.IsNullOrWhiteSpace(value) ? DefaultOtlpEndpoint : value;
        }

        /// <summary>
        /// OTLP wire protocol: <c>grpc</c> (default) or <c>http</c> (HTTP/protobuf). Null or empty values reset to the default.
        /// </summary>
        public string OtlpProtocol
        {
            get => _OtlpProtocol;
            set => _OtlpProtocol = string.IsNullOrWhiteSpace(value) ? DefaultOtlpProtocol : value;
        }

        /// <summary>
        /// Whether to serve an in-process Prometheus scrape endpoint. Default true.
        /// The endpoint is anonymous; keep it on an internal network.
        /// </summary>
        public bool PrometheusEnabled { get; set; } = true;

        /// <summary>
        /// Hostname the Prometheus endpoint binds to and answers for. Default <c>127.0.0.1</c> (loopback only).
        /// In a container, set it to the name Prometheus scrapes (the compose stack uses <c>redish</c>); requests
        /// must address the endpoint by this name. Wildcards (<c>*</c>, <c>+</c>) are rejected by the underlying
        /// OpenTelemetry listener. Null or empty values reset to the default.
        /// </summary>
        public string PrometheusHostname
        {
            get => _PrometheusHostname;
            set => _PrometheusHostname = string.IsNullOrWhiteSpace(value) ? DefaultPrometheusHostname : value;
        }

        /// <summary>
        /// Port of the Prometheus scrape endpoint (served at <c>/metrics</c>). Default 9464. Minimum 1, maximum 65535.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 1..65535.</exception>
        public int PrometheusPort
        {
            get => _PrometheusPort;
            set
            {
                if (value < 1 || value > 65535) throw new ArgumentOutOfRangeException(nameof(PrometheusPort), "PrometheusPort must be between 1 and 65535.");
                _PrometheusPort = value;
            }
        }

        /// <summary>
        /// Whether to push logs directly to Loki's OTLP endpoint. Default false.
        /// </summary>
        public bool LokiEnabled { get; set; } = false;

        /// <summary>
        /// Loki OTLP base endpoint. Default <c>http://127.0.0.1:3100/otlp</c>. Null or empty values reset to the default.
        /// </summary>
        public string LokiEndpoint
        {
            get => _LokiEndpoint;
            set => _LokiEndpoint = string.IsNullOrWhiteSpace(value) ? DefaultLokiEndpoint : value;
        }

        #endregion


        #region Private-Members

        private const string DefaultServiceName = "redish-server";
        private const string DefaultOtlpEndpoint = "http://127.0.0.1:4317";
        private const string DefaultOtlpProtocol = "grpc";
        private const string DefaultPrometheusHostname = "127.0.0.1";
        private const string DefaultLokiEndpoint = "http://127.0.0.1:3100/otlp";

        private string _ServiceName = DefaultServiceName;
        private double _TraceSamplingRatio = 1.0;
        private int _LogMinimumSeverity = 2;
        private string _OtlpEndpoint = DefaultOtlpEndpoint;
        private string _OtlpProtocol = DefaultOtlpProtocol;
        private string _PrometheusHostname = DefaultPrometheusHostname;
        private int _PrometheusPort = 9464;
        private string _LokiEndpoint = DefaultLokiEndpoint;

        #endregion


        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with default values.
        /// </summary>
        public TelemetrySettings()
        {
        }

        #endregion


        #region Public-Methods

        #endregion


        #region Private-Methods

        #endregion

    }
}
