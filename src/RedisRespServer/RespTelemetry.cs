namespace RedisResp
{
    using System;

    /// <summary>
    /// Stable public names for the telemetry emitted by the RedisRespServer library.
    /// </summary>
    /// <remarks>
    /// The library emits metrics through a <see cref="System.Diagnostics.Metrics.Meter"/> and traces through an
    /// <see cref="System.Diagnostics.ActivitySource"/>, both named <see cref="MeterName"/> /
    /// <see cref="ActivitySourceName"/>. It takes no dependency on any exporter or SDK; emission is effectively
    /// free until a host subscribes (for example with Radiant: <c>settings.Sources.AddMeter(RespTelemetry.MeterName)</c>
    /// and <c>settings.Sources.AddActivitySource(RespTelemetry.ActivitySourceName)</c>).
    /// These names are a public contract consumed by dashboards and alerts; they do not change between releases.
    /// All label values are drawn from small, fixed sets. Identifiers and payloads are never placed on metrics.
    /// This class is thread safe (it holds only constants).
    /// </remarks>
    public static class RespTelemetry
    {

        #region Public-Members

        /// <summary>
        /// Name of the meter used by the library: <c>RedisRespServer</c>.
        /// </summary>
        public const string MeterName = "RedisRespServer";

        /// <summary>
        /// Name of the activity source used by the library: <c>RedisRespServer</c>.
        /// </summary>
        public const string ActivitySourceName = "RedisRespServer";

        /// <summary>
        /// Up/down counter of listeners currently accepting connections. Unit <c>{listener}</c>.
        /// </summary>
        public const string ListenersActive = "redisresp.listeners.active";

        /// <summary>
        /// Up/down counter of client connections currently open. Unit <c>{connection}</c>.
        /// </summary>
        public const string ConnectionsActive = "redisresp.connections.active";

        /// <summary>
        /// Counter of client connections accepted. Unit <c>{connection}</c>.
        /// </summary>
        public const string ConnectionsAccepted = "redisresp.connections.accepted";

        /// <summary>
        /// Counter of client connections closed, labeled by <see cref="AttributeCloseReason"/>. Unit <c>{connection}</c>.
        /// </summary>
        public const string ConnectionsClosed = "redisresp.connections.closed";

        /// <summary>
        /// Histogram of client connection lifetimes, labeled by <see cref="AttributeCloseReason"/>. Unit <c>s</c>.
        /// </summary>
        public const string ConnectionDuration = "redisresp.connection.duration";

        /// <summary>
        /// Counter of failures accepting a client connection, labeled by <see cref="AttributeErrorType"/>. Unit <c>{error}</c>.
        /// </summary>
        public const string AcceptErrors = "redisresp.accept.errors";

        /// <summary>
        /// Counter of bytes read from client sockets. Unit <c>By</c>.
        /// </summary>
        public const string BytesReceived = "redisresp.network.received";

        /// <summary>
        /// Counter of complete RESP messages parsed, labeled by <see cref="AttributeRespType"/> and
        /// <see cref="AttributeRespProtocol"/>. Unit <c>{message}</c>.
        /// </summary>
        public const string MessagesReceived = "redisresp.messages.received";

        /// <summary>
        /// Histogram of complete RESP message sizes, labeled by <see cref="AttributeRespType"/>. Unit <c>By</c>.
        /// </summary>
        public const string MessageSize = "redisresp.message.size";

        /// <summary>
        /// Histogram of time spent dispatching one message to subscribers (event handlers run synchronously
        /// inside this window), labeled by <see cref="AttributeRespType"/> and <see cref="AttributeOutcome"/>. Unit <c>s</c>.
        /// </summary>
        public const string DispatchDuration = "redisresp.dispatch.duration";

        /// <summary>
        /// Counter of input the parser could not interpret (for example an unknown type prefix), labeled by
        /// <see cref="AttributeErrorType"/>. Unit <c>{error}</c>.
        /// </summary>
        public const string ParseErrors = "redisresp.parse.errors";

        /// <summary>
        /// Counter of unexpected errors on a client connection loop, labeled by <see cref="AttributeErrorType"/>. Unit <c>{error}</c>.
        /// </summary>
        public const string ClientErrors = "redisresp.client.errors";

        /// <summary>
        /// Prefix of the server span opened for every dispatched RESP message. The full span name is
        /// <c>resp.dispatch {RespDataType}</c>, for example <c>resp.dispatch Array</c>.
        /// </summary>
        public const string SpanDispatch = "resp.dispatch";

        /// <summary>
        /// RESP data type of a message (a <see cref="RespDataType"/> name, or <c>Unknown</c>).
        /// </summary>
        public const string AttributeRespType = "resp.type";

        /// <summary>
        /// RESP protocol generation of a message: <c>RESP2</c>, <c>RESP3</c>, or <c>Unknown</c>.
        /// </summary>
        public const string AttributeRespProtocol = "resp.protocol";

        /// <summary>
        /// Outcome of an operation: <c>ok</c>, <c>error</c>, or <c>unhandled</c> (the message was parsed but matched no dispatch path).
        /// </summary>
        public const string AttributeOutcome = "outcome";

        /// <summary>
        /// Why a connection closed: <c>client_closed</c>, <c>client_reset</c>, <c>server_disconnect</c>,
        /// <c>server_shutdown</c>, or <c>error</c>.
        /// </summary>
        public const string AttributeCloseReason = "close.reason";

        /// <summary>
        /// OpenTelemetry <c>error.type</c>: an exception type name or a short fixed code such as <c>unknown_prefix</c>.
        /// </summary>
        public const string AttributeErrorType = "error.type";

        /// <summary>
        /// Span-only attribute: the library-assigned client GUID. Never used as a metric label.
        /// </summary>
        public const string AttributeClientId = "resp.client.id";

        /// <summary>
        /// Span-only attribute: the size of the message in bytes.
        /// </summary>
        public const string AttributeMessageSize = "resp.message.size";

        /// <summary>
        /// Span-only attribute: the remote client address (OpenTelemetry <c>client.address</c>).
        /// </summary>
        public const string AttributeClientAddress = "client.address";

        /// <summary>
        /// Span-only attribute: the remote client port (OpenTelemetry <c>client.port</c>).
        /// </summary>
        public const string AttributeClientPort = "client.port";

        /// <summary>
        /// Outcome value for a successful operation.
        /// </summary>
        public const string OutcomeOk = "ok";

        /// <summary>
        /// Outcome value for a failed operation.
        /// </summary>
        public const string OutcomeError = "error";

        /// <summary>
        /// Outcome value for a message that was parsed but matched no dispatch path.
        /// </summary>
        public const string OutcomeUnhandled = "unhandled";

        /// <summary>
        /// Close reason: the client closed the connection cleanly.
        /// </summary>
        public const string CloseClientClosed = "client_closed";

        /// <summary>
        /// Close reason: the connection was reset or aborted by the peer.
        /// </summary>
        public const string CloseClientReset = "client_reset";

        /// <summary>
        /// Close reason: the server disconnected the client explicitly.
        /// </summary>
        public const string CloseServerDisconnect = "server_disconnect";

        /// <summary>
        /// Close reason: the listener was stopped.
        /// </summary>
        public const string CloseServerShutdown = "server_shutdown";

        /// <summary>
        /// Close reason: an unexpected error ended the connection loop.
        /// </summary>
        public const string CloseError = "error";

        /// <summary>
        /// Parse error type: the message began with a byte that is not a RESP2 or RESP3 type prefix.
        /// </summary>
        public const string ErrorUnknownPrefix = "unknown_prefix";

        /// <summary>
        /// Label value used when a RESP type or protocol could not be determined.
        /// </summary>
        public const string Unknown = "Unknown";
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
