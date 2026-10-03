namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One measurement captured by <see cref="TelemetryCapture"/>.
    /// </summary>
    public sealed class CapturedMeasurement
    {

        #region Public-Members

        /// <summary>
        /// Gets the instrument name.
        /// </summary>
        public string Instrument { get; }

        /// <summary>
        /// Gets the instrument unit.
        /// </summary>
        public string Unit { get; }

        /// <summary>
        /// Gets the recorded value.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Gets the labels, with values rendered as invariant strings.
        /// </summary>
        public IReadOnlyDictionary<string, string> Tags { get; }

        #endregion


        #region Private-Members

        #endregion


        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="CapturedMeasurement"/> class.
        /// </summary>
        /// <param name="instrument">The instrument name.</param>
        /// <param name="unit">The instrument unit.</param>
        /// <param name="value">The recorded value.</param>
        /// <param name="tags">The labels.</param>
        public CapturedMeasurement(string instrument, string unit, double value, IReadOnlyDictionary<string, string> tags)
        {
            Instrument = instrument;
            Unit = unit;
            Value = value;
            Tags = tags ?? new Dictionary<string, string>();
        }

        #endregion


        #region Public-Methods

        /// <summary>
        /// Returns true when every given label key/value pair is present.
        /// </summary>
        /// <param name="tags">Alternating label keys and values.</param>
        /// <returns>True when all pairs match.</returns>
        public bool Matches(params string[] tags)
        {
            if (tags == null) return true;
            for (int i = 0; i + 1 < tags.Length; i += 2)
            {
                if (!Tags.TryGetValue(tags[i], out string value) || value != tags[i + 1]) return false;
            }
            return true;
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
