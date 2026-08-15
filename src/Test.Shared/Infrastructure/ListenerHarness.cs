namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RedisResp;

    /// <summary>
    /// Wraps a <see cref="RespListener"/> for integration tests, capturing every raised event
    /// and providing convenient async waiters over the captured data.
    /// </summary>
    /// <remarks>
    /// The harness subscribes to all typed RESP data events as well as connection and error
    /// events. Captured data-received arguments are stored in a thread-safe list and a semaphore
    /// is released for each capture so that tests can deterministically wait for the next message
    /// rather than relying on fixed delays.
    /// </remarks>
    public sealed class ListenerHarness : IDisposable
    {

        #region Public-Members

        /// <summary>
        /// Gets the underlying listener under test.
        /// </summary>
        /// <value>The <see cref="RespListener"/> instance created by this harness.</value>
        public RespListener Listener
        {
            get { return _Listener; }
        }

        /// <summary>
        /// Gets the port the listener is bound to.
        /// </summary>
        /// <value>The TCP port number.</value>
        public int Port
        {
            get { return _Port; }
        }

        /// <summary>
        /// Gets a snapshot of all captured RESP data-received events across every type.
        /// </summary>
        /// <value>A copy of the captured events in arrival order.</value>
        public IReadOnlyList<RespDataReceivedEventArgs> Data
        {
            get { lock (_Lock) { return _Data.ToList(); } }
        }

        /// <summary>
        /// Gets the number of client-connected events observed.
        /// </summary>
        /// <value>The connection count.</value>
        public int ConnectedEvents
        {
            get { return Volatile.Read(ref _ConnectedEvents); }
        }

        /// <summary>
        /// Gets the number of client-disconnected events observed.
        /// </summary>
        /// <value>The disconnection count.</value>
        public int DisconnectedEvents
        {
            get { return Volatile.Read(ref _DisconnectedEvents); }
        }

        /// <summary>
        /// Gets a snapshot of all error events observed by the harness.
        /// </summary>
        /// <value>A copy of the captured error events.</value>
        public IReadOnlyList<RedisResp.ErrorEventArgs> Errors
        {
            get { lock (_Lock) { return _Errors.ToList(); } }
        }

        #endregion


        #region Private-Members

        private readonly RespListener _Listener;
        private readonly int _Port;
        private readonly object _Lock = new object();
        private readonly List<RespDataReceivedEventArgs> _Data = new List<RespDataReceivedEventArgs>();
        private readonly List<RedisResp.ErrorEventArgs> _Errors = new List<RedisResp.ErrorEventArgs>();
        private readonly SemaphoreSlim _DataSignal = new SemaphoreSlim(0);
        private int _ConnectedEvents;
        private int _DisconnectedEvents;

        #endregion


        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="ListenerHarness"/> class on a free port.
        /// </summary>
        public ListenerHarness()
        {
            _Port = PortAllocator.GetFreePort();
            _Listener = new RespListener(_Port);
            Subscribe();
        }

        /// <summary>
        /// Creates a harness whose listener has been started and is ready to accept connections.
        /// </summary>
        /// <returns>A started harness.</returns>
        public static async Task<ListenerHarness> StartAsync()
        {
            ListenerHarness harness = new ListenerHarness();
            await harness._Listener.StartAsync().ConfigureAwait(false);
            return harness;
        }

        #endregion


        #region Public-Methods

        /// <summary>
        /// Connects a raw client to the harness listener.
        /// </summary>
        /// <returns>A connected raw client owned by the caller.</returns>
        public async Task<RespRawClient> ConnectClientAsync()
        {
            RespRawClient client = new RespRawClient();
            await client.ConnectAsync("127.0.0.1", _Port).ConfigureAwait(false);
            return client;
        }

        /// <summary>
        /// Waits until a captured data event satisfies the predicate, or the timeout elapses.
        /// </summary>
        /// <param name="predicate">The predicate that identifies the desired event.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <returns>The matching event, or null if the timeout elapsed.</returns>
        public async Task<RespDataReceivedEventArgs> WaitForAsync(
            Func<RespDataReceivedEventArgs, bool> predicate, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);

            while (true)
            {
                lock (_Lock)
                {
                    RespDataReceivedEventArgs match = _Data.FirstOrDefault(predicate);
                    if (match != null) return match;
                }

                TimeSpan remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) return null;

                await _DataSignal.WaitAsync(remaining).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Waits until at least the specified number of data events have been captured.
        /// </summary>
        /// <param name="count">The minimum number of captured events.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <returns>True if the count was reached; otherwise, false.</returns>
        public async Task<bool> WaitForCountAsync(int count, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);

            while (true)
            {
                lock (_Lock)
                {
                    if (_Data.Count >= count) return true;
                }

                TimeSpan remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) return false;

                await _DataSignal.WaitAsync(remaining).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Waits until the listener reports the expected number of connected clients.
        /// </summary>
        /// <param name="expected">The expected connected-clients count.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <returns>True if the count was reached; otherwise, false.</returns>
        public async Task<bool> WaitForConnectedCountAsync(int expected, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                if (_Listener.ConnectedClientsCount == expected) return true;
                await Task.Delay(20).ConfigureAwait(false);
            }
            return _Listener.ConnectedClientsCount == expected;
        }

        /// <summary>
        /// Waits until the harness has observed the expected number of disconnect events.
        /// </summary>
        /// <param name="expected">The expected disconnect-event count.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <returns>True if the count was reached; otherwise, false.</returns>
        public async Task<bool> WaitForDisconnectedEventsAsync(int expected, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                if (DisconnectedEvents >= expected) return true;
                await Task.Delay(20).ConfigureAwait(false);
            }
            return DisconnectedEvents >= expected;
        }

        /// <summary>
        /// Clears all captured data and error events.
        /// </summary>
        public void Clear()
        {
            lock (_Lock)
            {
                _Data.Clear();
                _Errors.Clear();
            }
        }

        /// <summary>
        /// Stops the listener and releases all resources.
        /// </summary>
        public void Dispose()
        {
            try { _Listener.Dispose(); } catch { }
            _DataSignal.Dispose();
        }

        #endregion


        #region Private-Methods

        /// <summary>
        /// Subscribes to every event exposed by the listener.
        /// </summary>
        private void Subscribe()
        {
            _Listener.SimpleStringReceived += OnData;
            _Listener.ErrorReceived += OnData;
            _Listener.IntegerReceived += OnData;
            _Listener.BulkStringReceived += OnData;
            _Listener.ArrayReceived += OnData;
            _Listener.NullReceived += OnData;
            _Listener.DoubleReceived += OnData;
            _Listener.BooleanReceived += OnData;
            _Listener.BigNumberReceived += OnData;
            _Listener.BlobErrorReceived += OnData;
            _Listener.VerbatimStringReceived += OnData;
            _Listener.MapReceived += OnData;
            _Listener.SetReceived += OnData;
            _Listener.AttributeReceived += OnData;
            _Listener.PushReceived += OnData;

            _Listener.ClientConnected += (s, e) => Interlocked.Increment(ref _ConnectedEvents);
            _Listener.ClientDisconnected += (s, e) => Interlocked.Increment(ref _DisconnectedEvents);
            _Listener.ErrorOccurred += OnError;
        }

        /// <summary>
        /// Records a captured data event and signals waiters.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The captured event arguments.</param>
        private void OnData(object sender, RespDataReceivedEventArgs e)
        {
            lock (_Lock)
            {
                _Data.Add(e);
            }
            _DataSignal.Release();
        }

        /// <summary>
        /// Records a captured error event.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="e">The captured error arguments.</param>
        private void OnError(object sender, RedisResp.ErrorEventArgs e)
        {
            lock (_Lock)
            {
                _Errors.Add(e);
            }
        }

        #endregion

    }
}
