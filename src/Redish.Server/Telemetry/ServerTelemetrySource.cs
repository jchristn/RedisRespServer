namespace Redish.Server.Telemetry
{
    using System;
    using System.Collections.Generic;
    using RedisResp;

    /// <summary>
    /// Read-only view of one running server that the observable gauges sample at collection time.
    /// </summary>
    /// <remarks>
    /// Instances are registered with <see cref="RedishInstrumentation"/> when a server is constructed and
    /// unregistered when it is disposed. Callbacks run on the collector's thread and must be cheap and thread safe.
    /// </remarks>
    internal sealed class ServerTelemetrySource
    {

        #region Public-Members

        internal string StorageMode { get; }

        internal string RedisVersion { get; }

        internal int DatabaseCount { get; }

        internal DateTime StartedUtc { get; }

        #endregion


        #region Private-Members

        private readonly Func<long> _StorageKeyCount;
        private readonly Func<IEnumerable<RespVersionEnum>> _ClientProtocols;

        #endregion


        #region Constructors-and-Factories

        internal ServerTelemetrySource(
            string storageMode,
            string redisVersion,
            int databaseCount,
            DateTime startedUtc,
            Func<long> storageKeyCount,
            Func<IEnumerable<RespVersionEnum>> clientProtocols)
        {
            StorageMode = string.IsNullOrEmpty(storageMode) ? "unknown" : storageMode;
            RedisVersion = string.IsNullOrEmpty(redisVersion) ? "unknown" : redisVersion;
            DatabaseCount = databaseCount;
            StartedUtc = startedUtc;
            _StorageKeyCount = storageKeyCount ?? throw new ArgumentNullException(nameof(storageKeyCount));
            _ClientProtocols = clientProtocols ?? throw new ArgumentNullException(nameof(clientProtocols));
        }

        #endregion


        #region Public-Methods

        internal long StorageKeyCount()
        {
            return _StorageKeyCount();
        }

        internal IEnumerable<RespVersionEnum> ClientProtocols()
        {
            return _ClientProtocols();
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
