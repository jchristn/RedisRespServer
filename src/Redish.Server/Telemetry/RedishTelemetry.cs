namespace Redish.Server.Telemetry
{
    using System;

    /// <summary>
    /// Stable public names for the telemetry emitted by the Redish server: meter and activity-source names,
    /// metric names, span names, and attribute keys and values.
    /// </summary>
    /// <remarks>
    /// These names are a contract consumed by the Grafana dashboards in <c>assets/grafana/</c> and by alert rules.
    /// Prometheus renders dotted names in snake case with unit and <c>_total</c> suffixes (for example
    /// <c>redish.command.duration</c> becomes <c>redish_command_duration_seconds</c>). Every label is drawn from a
    /// small fixed set; client ids, key names, and values appear only on spans or never.
    /// This class is thread safe (it holds only constants).
    /// </remarks>
    public static class RedishTelemetry
    {

        #region Public-Members

        /// <summary>
        /// Meter name for Redish server metrics: <c>Redish.Server</c>.
        /// </summary>
        public const string MeterName = "Redish.Server";

        /// <summary>
        /// Activity source name for Redish server spans: <c>Redish.Server</c>.
        /// </summary>
        public const string ActivitySourceName = "Redish.Server";

        /// <summary>
        /// Counter of commands processed, by <see cref="AttributeOperation"/> and <see cref="AttributeOutcome"/>. Unit <c>{command}</c>.
        /// </summary>
        public const string Commands = "redish.commands";

        /// <summary>
        /// Histogram of end-to-end command latency (execute plus respond), by <see cref="AttributeOperation"/> and <see cref="AttributeOutcome"/>. Unit <c>s</c>.
        /// </summary>
        public const string CommandDuration = "redish.command.duration";

        /// <summary>
        /// Histogram of per-stage command latency, by <see cref="AttributeStage"/> (<c>execute</c>, <c>respond</c>) and <see cref="AttributeOperation"/>. Unit <c>s</c>.
        /// </summary>
        public const string CommandStageDuration = "redish.command.stage.duration";

        /// <summary>
        /// Counter of failed commands (error replies and exceptions), by <see cref="AttributeOperation"/> and <see cref="AttributeErrorType"/>. Unit <c>{error}</c>.
        /// </summary>
        public const string CommandErrors = "redish.command.errors";

        /// <summary>
        /// Histogram of reply sizes written to clients, by <see cref="AttributeOperation"/>. Unit <c>By</c>.
        /// </summary>
        public const string ResponseSize = "redish.response.size";

        /// <summary>
        /// Counter of replies that could not be delivered, by <see cref="AttributeErrorType"/> (<c>client_gone</c> or an exception type). Unit <c>{error}</c>.
        /// </summary>
        public const string ResponseErrors = "redish.response.errors";

        /// <summary>
        /// Counter of single-key lookups (GET, HGET, JSON.GET, ZSCORE), by <see cref="AttributeOperation"/> and <see cref="AttributeLookupResult"/>. Unit <c>{lookup}</c>.
        /// </summary>
        public const string KeyspaceLookups = "redish.keyspace.lookups";

        /// <summary>
        /// Counter of AUTH attempts, by <see cref="AttributeOutcome"/> (<c>success</c>, <c>failure</c>, <c>not_required</c>, <c>invalid_arguments</c>). Unit <c>{attempt}</c>.
        /// </summary>
        public const string AuthAttempts = "redish.auth.attempts";

        /// <summary>
        /// Gauge of clients tracked by the server, by negotiated <see cref="AttributeRespProtocol"/>. Unit <c>{client}</c>.
        /// </summary>
        public const string Clients = "redish.clients";

        /// <summary>
        /// Gauge of keys currently held in storage (including expired keys not yet swept). Unit <c>{key}</c>.
        /// </summary>
        public const string StorageKeys = "redish.storage.keys";

        /// <summary>
        /// Counter of expiration sweep jobs, by <see cref="AttributeOutcome"/> (<c>success</c>, <c>failure</c>). Unit <c>{sweep}</c>.
        /// </summary>
        public const string ExpirationSweeps = "redish.expiration.sweeps";

        /// <summary>
        /// Histogram of expiration sweep duration (queued through evict), by <see cref="AttributeOutcome"/>. Unit <c>s</c>.
        /// </summary>
        public const string ExpirationSweepDuration = "redish.expiration.sweep.duration";

        /// <summary>
        /// Histogram of per-stage expiration sweep duration, by <see cref="AttributeStage"/> (<c>queued</c>, <c>snapshot</c>, <c>evict</c>) and <see cref="AttributeOutcome"/>. Unit <c>s</c>.
        /// </summary>
        public const string ExpirationStageDuration = "redish.expiration.stage.duration";

        /// <summary>
        /// Counter of expiration sweep stage executions, by <see cref="AttributeStage"/> and <see cref="AttributeOutcome"/>. Unit <c>{stage}</c>.
        /// </summary>
        public const string ExpirationStageEvents = "redish.expiration.stage.events";

        /// <summary>
        /// Up/down counter of expiration sweeps running right now. Values above 1 mean sweeps overlap (a sweep takes longer than the 1 s interval). Unit <c>{sweep}</c>.
        /// </summary>
        public const string ExpirationSweepsActive = "redish.expiration.sweeps.active";

        /// <summary>
        /// Counter of keys examined by expiration sweeps. Unit <c>{key}</c>.
        /// </summary>
        public const string ExpirationKeysScanned = "redish.expiration.keys.scanned";

        /// <summary>
        /// Counter of keys removed because their TTL elapsed, by <see cref="AttributeTrigger"/> (<c>active</c> sweep or <c>passive</c> on access). Unit <c>{key}</c>.
        /// </summary>
        public const string ExpirationKeysExpired = "redish.expiration.keys.expired";

        /// <summary>
        /// Gauge of the Unix time (seconds) of the last successful expiration sweep; 0 before the first. Unit <c>s</c>.
        /// </summary>
        public const string ExpirationLastSuccess = "redish.expiration.last_success";

        /// <summary>
        /// Gauge fixed at 1 whose labels carry build and safe configuration facts:
        /// <see cref="AttributeServiceVersion"/>, <see cref="AttributeStorageMode"/>, <see cref="AttributeRedisVersion"/>. Unit <c>{info}</c>.
        /// </summary>
        public const string BuildInfo = "redish.build.info";

        /// <summary>
        /// Gauge of the configured logical database count. Unit <c>{database}</c>.
        /// </summary>
        public const string ConfigDatabases = "redish.config.databases";

        /// <summary>
        /// Gauge of seconds since the server started. Unit <c>s</c>.
        /// </summary>
        public const string Uptime = "redish.uptime";

        /// <summary>
        /// Prefix of the span opened per command: <c>redish.command {OPERATION}</c>, for example <c>redish.command GET</c>.
        /// </summary>
        public const string SpanCommand = "redish.command";

        /// <summary>
        /// Name of the root span opened per expiration sweep that expired keys or failed. Idle sweeps emit metrics only.
        /// </summary>
        public const string SpanExpirationSweep = "redish.expiration.sweep";

        /// <summary>
        /// Prefix of stage spans: <c>stage:{name}</c>, for example <c>stage:execute</c>.
        /// </summary>
        public const string SpanStagePrefix = "stage:";

        /// <summary>
        /// Command name (OpenTelemetry <c>db.operation.name</c>), upper case. Commands outside the supported set are reported as <c>UNKNOWN</c>.
        /// </summary>
        public const string AttributeOperation = "db.operation.name";

        /// <summary>
        /// Database system (OpenTelemetry <c>db.system.name</c>); always <c>redis</c>. Span only.
        /// </summary>
        public const string AttributeDbSystem = "db.system.name";

        /// <summary>
        /// Outcome of an operation. Commands: <c>ok</c>, <c>error</c> (an error reply), <c>exception</c>, <c>unknown_command</c>.
        /// </summary>
        public const string AttributeOutcome = "outcome";

        /// <summary>
        /// OpenTelemetry <c>error.type</c>: the error reply code (<c>ERR</c>, <c>WRONGTYPE</c>, <c>WRONGPASS</c>, <c>NOAUTH</c>, <c>NOPERM</c>, <c>OTHER</c>),
        /// an exception type name, or <c>client_gone</c>.
        /// </summary>
        public const string AttributeErrorType = "error.type";

        /// <summary>
        /// Workflow stage name.
        /// </summary>
        public const string AttributeStage = "redish.stage";

        /// <summary>
        /// Keyspace lookup result: <c>hit</c> or <c>miss</c>.
        /// </summary>
        public const string AttributeLookupResult = "result";

        /// <summary>
        /// Expiration trigger: <c>active</c> or <c>passive</c>.
        /// </summary>
        public const string AttributeTrigger = "trigger";

        /// <summary>
        /// RESP protocol generation: <c>RESP2</c> or <c>RESP3</c>.
        /// </summary>
        public const string AttributeRespProtocol = "resp.protocol";

        /// <summary>
        /// Server version (OpenTelemetry <c>service.version</c>).
        /// </summary>
        public const string AttributeServiceVersion = "service.version";

        /// <summary>
        /// Configured storage mode, for example <c>Ram</c>.
        /// </summary>
        public const string AttributeStorageMode = "redish.storage.mode";

        /// <summary>
        /// Redis compatibility version reported by INFO.
        /// </summary>
        public const string AttributeRedisVersion = "redish.redis_version";

        /// <summary>
        /// Span-only: number of arguments after the command name.
        /// </summary>
        public const string AttributeArgumentCount = "redish.args.count";

        /// <summary>
        /// Span-only: reply size in bytes.
        /// </summary>
        public const string AttributeResponseSize = "redish.response.size";

        /// <summary>
        /// Span-only: library-assigned client GUID.
        /// </summary>
        public const string AttributeClientId = "resp.client.id";

        /// <summary>
        /// Span-only: keys examined by a sweep.
        /// </summary>
        public const string AttributeKeysScanned = "redish.keys.scanned";

        /// <summary>
        /// Span-only: keys expired by a sweep.
        /// </summary>
        public const string AttributeKeysExpired = "redish.keys.expired";

        /// <summary>
        /// Command stage: running the command against storage.
        /// </summary>
        public const string StageExecute = "execute";

        /// <summary>
        /// Command stage: writing the reply to the client socket.
        /// </summary>
        public const string StageRespond = "respond";

        /// <summary>
        /// Sweep stage: waiting for a thread-pool thread after the timer fired.
        /// </summary>
        public const string StageQueued = "queued";

        /// <summary>
        /// Sweep stage: copying the key list under the storage read lock.
        /// </summary>
        public const string StageSnapshot = "snapshot";

        /// <summary>
        /// Sweep stage: checking each key and removing expired ones.
        /// </summary>
        public const string StageEvict = "evict";

        /// <summary>
        /// Outcome: command succeeded.
        /// </summary>
        public const string OutcomeOk = "ok";

        /// <summary>
        /// Outcome: command returned an error reply.
        /// </summary>
        public const string OutcomeError = "error";

        /// <summary>
        /// Outcome: command threw an exception (the client receives <c>-ERR internal server error</c>).
        /// </summary>
        public const string OutcomeException = "exception";

        /// <summary>
        /// Outcome: command name is not supported.
        /// </summary>
        public const string OutcomeUnknownCommand = "unknown_command";

        /// <summary>
        /// Outcome: a job completed.
        /// </summary>
        public const string OutcomeSuccess = "success";

        /// <summary>
        /// Outcome: a job failed.
        /// </summary>
        public const string OutcomeFailure = "failure";

        /// <summary>
        /// AUTH outcome: no authenticator is configured, so the attempt was accepted.
        /// </summary>
        public const string OutcomeNotRequired = "not_required";

        /// <summary>
        /// AUTH outcome: wrong number of arguments.
        /// </summary>
        public const string OutcomeInvalidArguments = "invalid_arguments";

        /// <summary>
        /// Operation label used for commands outside the supported set.
        /// </summary>
        public const string OperationUnknown = "UNKNOWN";

        /// <summary>
        /// Error type for a reply addressed to a client that is no longer connected.
        /// </summary>
        public const string ErrorClientGone = "client_gone";

        /// <summary>
        /// Lookup result: the key held a value.
        /// </summary>
        public const string LookupHit = "hit";

        /// <summary>
        /// Lookup result: the key or field was absent.
        /// </summary>
        public const string LookupMiss = "miss";

        /// <summary>
        /// Expiration trigger: removed by the background sweep.
        /// </summary>
        public const string TriggerActive = "active";

        /// <summary>
        /// Expiration trigger: removed when accessed after its TTL elapsed.
        /// </summary>
        public const string TriggerPassive = "passive";

        #endregion


        #region Private-Members

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        #endregion


        #region Private-Methods

        #endregion

    }
}
