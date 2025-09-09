namespace RedisResp
{
    using System;

    /// <summary>
    /// Provides a higher-level interface for handling RESP protocol messages with both event-driven and functional approaches.
    /// </summary>
    /// <remarks>
    /// This class wraps a RespListener instance and provides multiple ways to handle RESP protocol messages:
    /// - Traditional event handlers (re-exposed from RespListener)
    /// - Functional handlers using Func, Action, and EventHandler properties
    /// This dual approach allows for flexible message handling based on application needs.
    /// </remarks>
    public class RespInterface : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Raised when a simple string (prefix: +) is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> SimpleStringReceived;

        /// <summary>
        /// Raised when an error message (prefix: -) is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> ErrorReceived;

        /// <summary>
        /// Raised when an integer value (prefix: :) is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> IntegerReceived;

        /// <summary>
        /// Raised when a bulk string (prefix: $) is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> BulkStringReceived;

        /// <summary>
        /// Raised when an array (prefix: *) is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> ArrayReceived;

        /// <summary>
        /// Raised when a null value is received from a client.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> NullReceived;

        /// <summary>
        /// Raised when any RESP data is received from a client, regardless of type.
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> DataReceived;

        /// <summary>
        /// Raised when a new client connects to the server.
        /// </summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;

        /// <summary>
        /// Raised when a client disconnects from the server.
        /// </summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>
        /// Raised when an error occurs in the server or during client handling.
        /// </summary>
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Raised when a double value (prefix: ,) is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> DoubleReceived;

        /// <summary>
        /// Raised when a boolean value (prefix: #) is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> BooleanReceived;

        /// <summary>
        /// Raised when a big number value (prefix: () is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> BigNumberReceived;

        /// <summary>
        /// Raised when a blob error (prefix: !) is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> BlobErrorReceived;

        /// <summary>
        /// Raised when a verbatim string (prefix: =) is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> VerbatimStringReceived;

        /// <summary>
        /// Raised when a map (prefix: %) is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> MapReceived;

        /// <summary>
        /// Raised when a set (prefix: ~) is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> SetReceived;

        /// <summary>
        /// Raised when an attribute (prefix: |) is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> AttributeReceived;

        /// <summary>
        /// Raised when a push message (prefix: >) is received from a client (RESP3).
        /// </summary>
        public event EventHandler<RespDataReceivedEventArgs> PushReceived;

        /// <summary>
        /// Gets or sets the functional handler for simple string messages.
        /// </summary>
        /// <value>A function that takes RespDataReceivedEventArgs and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> SimpleStringHandler { get; set; }

        /// <summary>
        /// Gets or sets the functional handler for error messages.
        /// </summary>
        /// <value>A function that takes RespDataReceivedEventArgs and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> ErrorHandler { get; set; }

        /// <summary>
        /// Gets or sets the functional handler for integer messages.
        /// </summary>
        /// <value>A function that takes RespDataReceivedEventArgs and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> IntegerHandler { get; set; }

        /// <summary>
        /// Gets or sets the functional handler for bulk string messages.
        /// </summary>
        /// <value>A function that takes RespDataReceivedEventArgs and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> BulkStringHandler { get; set; }

        /// <summary>
        /// Gets or sets the functional handler for array messages.
        /// </summary>
        /// <value>A function that takes RespDataReceivedEventArgs and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> ArrayHandler { get; set; }

        /// <summary>
        /// Gets or sets the functional handler for null messages.
        /// </summary>
        /// <value>A function that takes RespDataReceivedEventArgs and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> NullHandler { get; set; }

        /// <summary>
        /// Gets or sets the functional handler for all data messages.
        /// </summary>
        /// <value>A function that takes RespDataReceivedEventArgs and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> DataHandler { get; set; }

        /// <summary>
        /// Gets or sets the action handler for client connection events.
        /// </summary>
        /// <value>An action that processes ClientConnectedEventArgs, or null if no handler is set.</value>
        public Action<ClientConnectedEventArgs> ClientConnectedAction { get; set; }

        /// <summary>
        /// Gets or sets the action handler for client disconnection events.
        /// </summary>
        /// <value>An action that processes ClientDisconnectedEventArgs, or null if no handler is set.</value>
        public Action<ClientDisconnectedEventArgs> ClientDisconnectedAction { get; set; }

        /// <summary>
        /// Gets or sets the action handler for error events.
        /// </summary>
        /// <value>An action that processes ErrorEventArgs, or null if no handler is set.</value>
        public Action<ErrorEventArgs> ErrorAction { get; set; }

        /// <summary>
        /// Gets or sets the event handler for simple string messages.
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> SimpleStringEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the event handler for error messages.
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> ErrorEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the event handler for integer messages.
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> IntegerEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the event handler for bulk string messages.
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> BulkStringEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the event handler for array messages.
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> ArrayEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the event handler for null messages.
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> NullEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the event handler for all data messages.
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> DataEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the event handler for client connection events.
        /// </summary>
        /// <value>An EventHandler for ClientConnectedEventArgs, or null if no handler is set.</value>
        public EventHandler<ClientConnectedEventArgs> ClientConnectedEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the event handler for client disconnection events.
        /// </summary>
        /// <value>An EventHandler for ClientDisconnectedEventArgs, or null if no handler is set.</value>
        public EventHandler<ClientDisconnectedEventArgs> ClientDisconnectedEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the event handler for server error events.
        /// </summary>
        /// <value>An EventHandler for ErrorEventArgs, or null if no handler is set.</value>
        public EventHandler<ErrorEventArgs> ServerErrorEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the function handler for double values (RESP3).
        /// </summary>
        /// <value>A Func that processes double values and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> DoubleHandler { get; set; }

        /// <summary>
        /// Gets or sets the action handler for double values (RESP3).
        /// </summary>
        /// <value>An Action that processes double values, or null if no handler is set.</value>
        public Action<RespDataReceivedEventArgs> DoubleAction { get; set; }

        /// <summary>
        /// Gets or sets the event handler for double values (RESP3).
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> DoubleEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the function handler for boolean values (RESP3).
        /// </summary>
        /// <value>A Func that processes boolean values and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> BooleanHandler { get; set; }

        /// <summary>
        /// Gets or sets the action handler for boolean values (RESP3).
        /// </summary>
        /// <value>An Action that processes boolean values, or null if no handler is set.</value>
        public Action<RespDataReceivedEventArgs> BooleanAction { get; set; }

        /// <summary>
        /// Gets or sets the event handler for boolean values (RESP3).
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> BooleanEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the function handler for big number values (RESP3).
        /// </summary>
        /// <value>A Func that processes big number values and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> BigNumberHandler { get; set; }

        /// <summary>
        /// Gets or sets the action handler for big number values (RESP3).
        /// </summary>
        /// <value>An Action that processes big number values, or null if no handler is set.</value>
        public Action<RespDataReceivedEventArgs> BigNumberAction { get; set; }

        /// <summary>
        /// Gets or sets the event handler for big number values (RESP3).
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> BigNumberEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the function handler for blob error values (RESP3).
        /// </summary>
        /// <value>A Func that processes blob error values and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> BlobErrorHandler { get; set; }

        /// <summary>
        /// Gets or sets the action handler for blob error values (RESP3).
        /// </summary>
        /// <value>An Action that processes blob error values, or null if no handler is set.</value>
        public Action<RespDataReceivedEventArgs> BlobErrorAction { get; set; }

        /// <summary>
        /// Gets or sets the event handler for blob error values (RESP3).
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> BlobErrorEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the function handler for verbatim string values (RESP3).
        /// </summary>
        /// <value>A Func that processes verbatim string values and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> VerbatimStringHandler { get; set; }

        /// <summary>
        /// Gets or sets the action handler for verbatim string values (RESP3).
        /// </summary>
        /// <value>An Action that processes verbatim string values, or null if no handler is set.</value>
        public Action<RespDataReceivedEventArgs> VerbatimStringAction { get; set; }

        /// <summary>
        /// Gets or sets the event handler for verbatim string values (RESP3).
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> VerbatimStringEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the function handler for map values (RESP3).
        /// </summary>
        /// <value>A Func that processes map values and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> MapHandler { get; set; }

        /// <summary>
        /// Gets or sets the action handler for map values (RESP3).
        /// </summary>
        /// <value>An Action that processes map values, or null if no handler is set.</value>
        public Action<RespDataReceivedEventArgs> MapAction { get; set; }

        /// <summary>
        /// Gets or sets the event handler for map values (RESP3).
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> MapEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the function handler for set values (RESP3).
        /// </summary>
        /// <value>A Func that processes set values and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> SetHandler { get; set; }

        /// <summary>
        /// Gets or sets the action handler for set values (RESP3).
        /// </summary>
        /// <value>An Action that processes set values, or null if no handler is set.</value>
        public Action<RespDataReceivedEventArgs> SetAction { get; set; }

        /// <summary>
        /// Gets or sets the event handler for set values (RESP3).
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> SetEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the function handler for attribute values (RESP3).
        /// </summary>
        /// <value>A Func that processes attribute values and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> AttributeHandler { get; set; }

        /// <summary>
        /// Gets or sets the action handler for attribute values (RESP3).
        /// </summary>
        /// <value>An Action that processes attribute values, or null if no handler is set.</value>
        public Action<RespDataReceivedEventArgs> AttributeAction { get; set; }

        /// <summary>
        /// Gets or sets the event handler for attribute values (RESP3).
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> AttributeEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the function handler for push values (RESP3).
        /// </summary>
        /// <value>A Func that processes push values and returns a response string, or null if no handler is set.</value>
        public Func<RespDataReceivedEventArgs, string> PushHandler { get; set; }

        /// <summary>
        /// Gets or sets the action handler for push values (RESP3).
        /// </summary>
        /// <value>An Action that processes push values, or null if no handler is set.</value>
        public Action<RespDataReceivedEventArgs> PushAction { get; set; }

        /// <summary>
        /// Gets or sets the event handler for push values (RESP3).
        /// </summary>
        /// <value>An EventHandler for RespDataReceivedEventArgs, or null if no handler is set.</value>
        public EventHandler<RespDataReceivedEventArgs> PushEventHandler { get; set; }

        /// <summary>
        /// Gets or sets the authentication function that is called when a client attempts to authenticate.
        /// </summary>
        /// <value>A function that takes username and password parameters and returns true if authentication succeeds, or null if no authentication is required.</value>
        /// <remarks>
        /// If this function is not set (null), all authentication attempts will be allowed by default.
        /// The function receives the username (or null for password-only AUTH) and password as parameters.
        /// Return true to allow the authentication, false to deny it.
        /// </remarks>
        public Func<string, string, bool> Authenticate { get; set; }

        /// <summary>
        /// Gets the underlying RespListener instance.
        /// </summary>
        /// <value>The RespListener instance used for RESP protocol handling.</value>
        public RespListener Listener { get; private set; }

        #endregion

        #region Private-Members

        private readonly RespListener _listener;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="RespInterface"/> class.
        /// </summary>
        /// <param name="listener">The RespListener instance to wrap and manage.</param>
        /// <exception cref="ArgumentNullException">Thrown when listener is null.</exception>
        /// <remarks>
        /// Creates a new RespInterface that wraps the provided RespListener and subscribes
        /// to all its events, providing both traditional event handling and functional handling approaches.
        /// </remarks>
        public RespInterface(RespListener listener)
        {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            Listener = _listener;

            _listener.SimpleStringReceived += OnSimpleStringReceived;
            _listener.ErrorReceived += OnErrorReceived;
            _listener.IntegerReceived += OnIntegerReceived;
            _listener.BulkStringReceived += OnBulkStringReceived;
            _listener.ArrayReceived += OnArrayReceived;
            _listener.NullReceived += OnNullReceived;
            _listener.DoubleReceived += OnDoubleReceived;
            _listener.BooleanReceived += OnBooleanReceived;
            _listener.BigNumberReceived += OnBigNumberReceived;
            _listener.BlobErrorReceived += OnBlobErrorReceived;
            _listener.VerbatimStringReceived += OnVerbatimStringReceived;
            _listener.MapReceived += OnMapReceived;
            _listener.SetReceived += OnSetReceived;
            _listener.AttributeReceived += OnAttributeReceived;
            _listener.PushReceived += OnPushReceived;
            _listener.DataReceived += OnDataReceived;
            _listener.ClientConnected += OnClientConnected;
            _listener.ClientDisconnected += OnClientDisconnected;
            _listener.ErrorOccurred += OnErrorOccurred;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Releases all resources used by the <see cref="RespInterface"/>.
        /// </summary>
        /// <remarks>
        /// This method disposes of the underlying RespListener and unsubscribes from all events.
        /// After calling this method, the instance cannot be reused.
        /// </remarks>
        public void Dispose()
        {
            if (_listener != null)
            {
                _listener.SimpleStringReceived -= OnSimpleStringReceived;
                _listener.ErrorReceived -= OnErrorReceived;
                _listener.IntegerReceived -= OnIntegerReceived;
                _listener.BulkStringReceived -= OnBulkStringReceived;
                _listener.ArrayReceived -= OnArrayReceived;
                _listener.NullReceived -= OnNullReceived;
                _listener.DoubleReceived -= OnDoubleReceived;
                _listener.BooleanReceived -= OnBooleanReceived;
                _listener.BigNumberReceived -= OnBigNumberReceived;
                _listener.BlobErrorReceived -= OnBlobErrorReceived;
                _listener.VerbatimStringReceived -= OnVerbatimStringReceived;
                _listener.MapReceived -= OnMapReceived;
                _listener.SetReceived -= OnSetReceived;
                _listener.AttributeReceived -= OnAttributeReceived;
                _listener.PushReceived -= OnPushReceived;
                _listener.DataReceived -= OnDataReceived;
                _listener.ClientConnected -= OnClientConnected;
                _listener.ClientDisconnected -= OnClientDisconnected;
                _listener.ErrorOccurred -= OnErrorOccurred;
                
                _listener.Dispose();
            }
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Handles simple string received events from the underlying RespListener.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the simple string information.</param>
        private void OnSimpleStringReceived(object sender, RespDataReceivedEventArgs e)
        {
            SimpleStringReceived?.Invoke(this, e);
            SimpleStringHandler?.Invoke(e);
            SimpleStringEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles error received events from the underlying RespListener.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the error information.</param>
        private void OnErrorReceived(object sender, RespDataReceivedEventArgs e)
        {
            ErrorReceived?.Invoke(this, e);
            ErrorHandler?.Invoke(e);
            ErrorEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles integer received events from the underlying RespListener.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the integer information.</param>
        private void OnIntegerReceived(object sender, RespDataReceivedEventArgs e)
        {
            IntegerReceived?.Invoke(this, e);
            IntegerHandler?.Invoke(e);
            IntegerEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles bulk string received events from the underlying RespListener.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the bulk string information.</param>
        private void OnBulkStringReceived(object sender, RespDataReceivedEventArgs e)
        {
            BulkStringReceived?.Invoke(this, e);
            BulkStringHandler?.Invoke(e);
            BulkStringEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles array received events from the underlying RespListener.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the array information.</param>
        private void OnArrayReceived(object sender, RespDataReceivedEventArgs e)
        {
            ArrayReceived?.Invoke(this, e);
            ArrayHandler?.Invoke(e);
            ArrayEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles null received events from the underlying RespListener.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the null value information.</param>
        private void OnNullReceived(object sender, RespDataReceivedEventArgs e)
        {
            NullReceived?.Invoke(this, e);
            NullHandler?.Invoke(e);
            NullEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles data received events from the underlying RespListener.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the received data information.</param>
        private void OnDataReceived(object sender, RespDataReceivedEventArgs e)
        {
            DataReceived?.Invoke(this, e);
            DataHandler?.Invoke(e);
            DataEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles client connected events from the underlying RespListener.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the client connection information.</param>
        private void OnClientConnected(object sender, ClientConnectedEventArgs e)
        {
            ClientConnected?.Invoke(this, e);
            ClientConnectedAction?.Invoke(e);
            ClientConnectedEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles client disconnected events from the underlying RespListener.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the client disconnection information.</param>
        private void OnClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            ClientDisconnected?.Invoke(this, e);
            ClientDisconnectedAction?.Invoke(e);
            ClientDisconnectedEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles error occurred events from the underlying RespListener.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the error information.</param>
        private void OnErrorOccurred(object sender, ErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
            ErrorAction?.Invoke(e);
            ServerErrorEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles double received events from the underlying RespListener (RESP3).
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the double information.</param>
        private void OnDoubleReceived(object sender, RespDataReceivedEventArgs e)
        {
            DoubleReceived?.Invoke(this, e);
            DoubleHandler?.Invoke(e);
            DoubleAction?.Invoke(e);
            DoubleEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles boolean received events from the underlying RespListener (RESP3).
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the boolean information.</param>
        private void OnBooleanReceived(object sender, RespDataReceivedEventArgs e)
        {
            BooleanReceived?.Invoke(this, e);
            BooleanHandler?.Invoke(e);
            BooleanAction?.Invoke(e);
            BooleanEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles big number received events from the underlying RespListener (RESP3).
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the big number information.</param>
        private void OnBigNumberReceived(object sender, RespDataReceivedEventArgs e)
        {
            BigNumberReceived?.Invoke(this, e);
            BigNumberHandler?.Invoke(e);
            BigNumberAction?.Invoke(e);
            BigNumberEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles blob error received events from the underlying RespListener (RESP3).
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the blob error information.</param>
        private void OnBlobErrorReceived(object sender, RespDataReceivedEventArgs e)
        {
            BlobErrorReceived?.Invoke(this, e);
            BlobErrorHandler?.Invoke(e);
            BlobErrorAction?.Invoke(e);
            BlobErrorEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles verbatim string received events from the underlying RespListener (RESP3).
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the verbatim string information.</param>
        private void OnVerbatimStringReceived(object sender, RespDataReceivedEventArgs e)
        {
            VerbatimStringReceived?.Invoke(this, e);
            VerbatimStringHandler?.Invoke(e);
            VerbatimStringAction?.Invoke(e);
            VerbatimStringEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles map received events from the underlying RespListener (RESP3).
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the map information.</param>
        private void OnMapReceived(object sender, RespDataReceivedEventArgs e)
        {
            MapReceived?.Invoke(this, e);
            MapHandler?.Invoke(e);
            MapAction?.Invoke(e);
            MapEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles set received events from the underlying RespListener (RESP3).
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the set information.</param>
        private void OnSetReceived(object sender, RespDataReceivedEventArgs e)
        {
            SetReceived?.Invoke(this, e);
            SetHandler?.Invoke(e);
            SetAction?.Invoke(e);
            SetEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles attribute received events from the underlying RespListener (RESP3).
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the attribute information.</param>
        private void OnAttributeReceived(object sender, RespDataReceivedEventArgs e)
        {
            AttributeReceived?.Invoke(this, e);
            AttributeHandler?.Invoke(e);
            AttributeAction?.Invoke(e);
            AttributeEventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles push received events from the underlying RespListener (RESP3).
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data containing the push information.</param>
        private void OnPushReceived(object sender, RespDataReceivedEventArgs e)
        {
            PushReceived?.Invoke(this, e);
            PushHandler?.Invoke(e);
            PushAction?.Invoke(e);
            PushEventHandler?.Invoke(this, e);
        }

        #endregion
    }
}