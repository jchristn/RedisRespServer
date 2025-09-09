namespace Redish.Server.Settings
{
    using System;
    using SyslogLogging;

    /// <summary>
    /// Configuration settings for the Redish server.
    /// </summary>
    /// <remarks>
    /// This class contains all configuration options for running the Redish server,
    /// including console output, network settings, SSL support, and syslog logging.
    /// </remarks>
    public class ServerSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether console output is enabled.
        /// </summary>
        /// <value>True if console output should be displayed; false to suppress console output.</value>
        public bool EnableConsole { get; set; } = true;

        /// <summary>
        /// Gets or sets the port number on which the server will listen.
        /// </summary>
        /// <value>The port number (default: 6379 for Redis compatibility).</value>
        public int Port
        {
            get => _Port;
            set => _Port = (value < 0 || value > 65535 ? throw new ArgumentOutOfRangeException(nameof(Port)) : value);
        }

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging
        {
            get => _Logging;
            set
            {
                if (value == null) value = new LoggingSettings();
                _Logging = value;
            }
        }

        /// <summary>
        /// Storage settings.
        /// </summary>
        public StorageSettings Storage
        {
            get => _Storage;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Storage));
                _Storage = value;
            }
        }

        /// <summary>
        /// Gets or sets the Redis compatibility version to report in INFO commands.
        /// </summary>
        /// <value>The Redis version string (default: "7.0.0").</value>
        public string RedisCompatibilityVersion { get; set; } = "7.0.0";

        /// <summary>
        /// Gets or sets the number of databases supported by the server.
        /// </summary>
        /// <value>The number of logical databases (default: 16).</value>
        public int DatabaseCount { get; set; } = 16;

        /// <summary>
        /// Gets or sets the replication backlog size in bytes.
        /// </summary>
        /// <value>The backlog buffer size for replication (default: 1048576 bytes = 1MB).</value>
        public long ReplicationBacklogSize { get; set; } = 1048576;

        /// <summary>
        /// Gets or sets the sentinel down-after-milliseconds configuration.
        /// </summary>
        /// <value>Time in milliseconds before considering a master down (default: 30000ms = 30s).</value>
        public int SentinelDownAfterMilliseconds { get; set; } = 30000;

        /// <summary>
        /// Gets or sets a value indicating whether slave instances are read-only.
        /// </summary>
        /// <value>True if slaves should be read-only; false otherwise (default: true).</value>
        public bool SlaveReadOnly { get; set; } = true;

        /// <summary>
        /// Gets or sets the server identification string.
        /// </summary>
        /// <value>A unique identifier for this server instance.</value>
        public string ServerId { get; set; } = Guid.NewGuid().ToString("N");

        private LoggingSettings _Logging = new LoggingSettings();
        private StorageSettings _Storage = new StorageSettings();
        private int _Port = 6379;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSettings"/> class.
        /// </summary>
        /// <remarks>
        /// Creates a new Settings instance with default values for all properties.
        /// Console output is enabled by default, the server listens on all interfaces
        /// on port 6379, SSL is disabled, and syslog logging is disabled.
        /// </remarks>
        public ServerSettings()
        {
        }
    }
}