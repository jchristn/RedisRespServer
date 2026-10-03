namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Redish.Server.Storage;

    /// <summary>
    /// A <see cref="DictionaryStorage"/> that lets a test run the expiration sweep on demand and, optionally,
    /// makes the sweep's snapshot stage fail so the failure path can be observed.
    /// </summary>
    public sealed class SweepableStorage : DictionaryStorage
    {

        #region Public-Members

        /// <summary>
        /// Gets or sets whether enumerating keys throws, which fails the sweep's snapshot stage.
        /// </summary>
        public bool FailKeyEnumeration { get; set; }

        #endregion


        #region Private-Members

        #endregion


        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="SweepableStorage"/> class.
        /// </summary>
        public SweepableStorage() : base()
        {
        }

        #endregion


        #region Public-Methods

        /// <summary>
        /// Runs one expiration sweep and waits for it to finish.
        /// </summary>
        /// <returns>A task that completes when the sweep has finished.</returns>
        public Task RunSweepAsync()
        {
            return CleanupExpiredKeysAsync();
        }

        /// <summary>
        /// Returns all keys, or throws when <see cref="FailKeyEnumeration"/> is set.
        /// </summary>
        /// <returns>The keys.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="FailKeyEnumeration"/> is set.</exception>
        public override IEnumerable<string> GetAllKeys()
        {
            if (FailKeyEnumeration) throw new InvalidOperationException("Simulated key enumeration failure.");
            return base.GetAllKeys();
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
