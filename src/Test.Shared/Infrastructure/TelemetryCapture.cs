namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// In-memory listener that captures every measurement and completed span emitted on the meters and activity
    /// sources it is created for. Used to prove that telemetry is emitted without any exporter or SDK.
    /// </summary>
    /// <remarks>
    /// Listeners are process-wide, so a capture may also see telemetry from unrelated background work (for example
    /// another storage engine's expiration sweep). Assertions should filter by labels and use lower bounds.
    /// Thread safe.
    /// </remarks>
    public sealed class TelemetryCapture : IDisposable
    {

        #region Public-Members

        /// <summary>
        /// Gets a snapshot of the captured measurements.
        /// </summary>
        public IReadOnlyList<CapturedMeasurement> Measurements
        {
            get { lock (_Lock) { return _Measurements.ToList(); } }
        }

        /// <summary>
        /// Gets a snapshot of the captured, completed activities.
        /// </summary>
        public IReadOnlyList<Activity> Activities
        {
            get { lock (_Lock) { return _Activities.ToList(); } }
        }

        #endregion


        #region Private-Members

        private readonly object _Lock = new object();
        private readonly HashSet<string> _Sources;
        private readonly List<CapturedMeasurement> _Measurements = new List<CapturedMeasurement>();
        private readonly List<Activity> _Activities = new List<Activity>();
        private readonly MeterListener _MeterListener;
        private readonly ActivityListener _ActivityListener;

        #endregion


        #region Constructors-and-Factories

        /// <summary>
        /// Starts capturing the given meter and activity-source names.
        /// </summary>
        /// <param name="sourceNames">Names used for both meters and activity sources.</param>
        public TelemetryCapture(params string[] sourceNames)
        {
            _Sources = new HashSet<string>(sourceNames ?? new string[0], StringComparer.Ordinal);

            _MeterListener = new MeterListener();
            _MeterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (_Sources.Contains(instrument.Meter.Name)) listener.EnableMeasurementEvents(instrument);
            };
            _MeterListener.SetMeasurementEventCallback<long>((i, v, t, s) => Add(i, v, t));
            _MeterListener.SetMeasurementEventCallback<int>((i, v, t, s) => Add(i, v, t));
            _MeterListener.SetMeasurementEventCallback<double>((i, v, t, s) => Add(i, v, t));
            _MeterListener.Start();

            _ActivityListener = new ActivityListener
            {
                ShouldListenTo = source => _Sources.Contains(source.Name),
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    lock (_Lock) { _Activities.Add(activity); }
                }
            };
            ActivitySource.AddActivityListener(_ActivityListener);
        }

        #endregion


        #region Public-Methods

        /// <summary>
        /// Polls observable instruments (gauges) so their current values are captured.
        /// </summary>
        public void CollectObservables()
        {
            _MeterListener.RecordObservableInstruments();
        }

        /// <summary>
        /// Returns captured measurements for an instrument whose labels include every given key/value pair.
        /// </summary>
        /// <param name="instrument">The instrument name.</param>
        /// <param name="tags">Alternating label keys and values that must all match.</param>
        /// <returns>The matching measurements.</returns>
        public List<CapturedMeasurement> Find(string instrument, params string[] tags)
        {
            return Measurements.Where(m => m.Instrument == instrument && m.Matches(tags)).ToList();
        }

        /// <summary>
        /// Sums the values of matching measurements.
        /// </summary>
        /// <param name="instrument">The instrument name.</param>
        /// <param name="tags">Alternating label keys and values that must all match.</param>
        /// <returns>The sum, or zero when nothing matched.</returns>
        public double Sum(string instrument, params string[] tags)
        {
            return Find(instrument, tags).Sum(m => m.Value);
        }

        /// <summary>
        /// Counts matching measurements (for histograms, the number of recorded observations).
        /// </summary>
        /// <param name="instrument">The instrument name.</param>
        /// <param name="tags">Alternating label keys and values that must all match.</param>
        /// <returns>The number of matching measurements.</returns>
        public int Count(string instrument, params string[] tags)
        {
            return Find(instrument, tags).Count;
        }

        /// <summary>
        /// Waits until a condition over the captured data holds, or the timeout elapses.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="timeout">The maximum wait.</param>
        /// <returns>True if the condition held before the timeout.</returns>
        public async Task<bool> WaitUntilAsync(Func<TelemetryCapture, bool> condition, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition(this)) return true;
                await Task.Delay(25).ConfigureAwait(false);
            }
            return condition(this);
        }

        /// <summary>
        /// Returns captured activities whose display name equals or starts with the given text.
        /// </summary>
        /// <param name="namePrefix">The display name, or its prefix.</param>
        /// <returns>The matching activities.</returns>
        public List<Activity> FindActivities(string namePrefix)
        {
            return Activities.Where(a => a.DisplayName == namePrefix || a.DisplayName.StartsWith(namePrefix, StringComparison.Ordinal)).ToList();
        }

        /// <summary>
        /// Stops capturing.
        /// </summary>
        public void Dispose()
        {
            try { _MeterListener.Dispose(); } catch { }
            try { _ActivityListener.Dispose(); } catch { }
        }

        #endregion


        #region Private-Methods

        private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            Dictionary<string, string> copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> tag in tags)
            {
                copy[tag.Key] = tag.Value == null ? null : Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            CapturedMeasurement measurement = new CapturedMeasurement(instrument.Name, instrument.Unit, value, copy);
            lock (_Lock) { _Measurements.Add(measurement); }
        }

        #endregion

    }
}
