namespace Redish.Server.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using RedisResp;
    using Redish.Server.Models;

    /// <summary>
    /// Thread-safe dictionary-based implementation of Redis storage.
    /// </summary>
    /// <remarks>
    /// Uses ReaderWriterLockSlim for efficient concurrent read/write access
    /// to an underlying Dictionary for storing Redis values.
    /// </remarks>
    public class DictionaryStorage : StorageBase
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

        private readonly Dictionary<string, RedisValue> _Storage;
        private readonly ReaderWriterLockSlim _Lock;
        private bool _Disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DictionaryStorage"/> class.
        /// </summary>
        public DictionaryStorage() : base()
        {
            _Storage = new Dictionary<string, RedisValue>();
            _Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        /// <summary>
        /// Gets or sets a value with automatic expiration checking.
        /// </summary>
        /// <param name="key">The key of the value.</param>
        /// <returns>The value if it exists and hasn't expired; otherwise, null.</returns>
        public override RedisValue this[string key]
        {
            get
            {
                if (string.IsNullOrEmpty(key))
                    return null;

                _Lock.EnterReadLock();
                try
                {
                    RedisValue value;
                    if (_Storage.TryGetValue(key, out value))
                    {
                        if (value.IsExpired)
                        {
                            // Upgrade to write lock to remove expired item
                            _Lock.ExitReadLock();
                            _Lock.EnterWriteLock();
                            try
                            {
                                // Double-check after acquiring write lock
                                if (_Storage.TryGetValue(key, out value) && value.IsExpired)
                                {
                                    _Storage.Remove(key);
                                }
                                return null;
                            }
                            finally
                            {
                                _Lock.ExitWriteLock();
                            }
                        }
                        return value;
                    }
                    return null;
                }
                finally
                {
                    if (_Lock.IsReadLockHeld)
                        _Lock.ExitReadLock();
                }
            }
            set
            {
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentNullException(nameof(key));
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _Lock.EnterWriteLock();
                try
                {
                    _Storage[key] = value;
                }
                finally
                {
                    _Lock.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Gets the count of items in storage.
        /// </summary>
        public override int Count
        {
            get
            {
                _Lock.EnterReadLock();
                try
                {
                    return _Storage.Count;
                }
                finally
                {
                    _Lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Attempts to add a new key-value pair.
        /// </summary>
        /// <param name="key">The key to add.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>True if the key was added; false if it already exists.</returns>
        public override bool TryAdd(string key, RedisValue value)
        {
            if (string.IsNullOrEmpty(key) || value == null)
                return false;

            _Lock.EnterWriteLock();
            try
            {
                if (_Storage.ContainsKey(key))
                {
                    // Check if existing value is expired
                    if (_Storage[key].IsExpired)
                    {
                        _Storage[key] = value;
                        return true;
                    }
                    return false;
                }

                _Storage.Add(key, value);
                return true;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Attempts to get a value by key.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The value if found.</param>
        /// <returns>True if the key exists and hasn't expired.</returns>
        public override bool TryGetValue(string key, out RedisValue value)
        {
            value = null;

            if (string.IsNullOrEmpty(key))
                return false;

            _Lock.EnterReadLock();
            try
            {
                if (_Storage.TryGetValue(key, out value))
                {
                    if (value.IsExpired)
                    {
                        // Need to remove expired value
                        _Lock.ExitReadLock();
                        _Lock.EnterWriteLock();
                        try
                        {
                            // Double-check after acquiring write lock
                            if (_Storage.TryGetValue(key, out value) && value.IsExpired)
                            {
                                _Storage.Remove(key);
                                value = null;
                                return false;
                            }
                            return value != null;
                        }
                        finally
                        {
                            _Lock.ExitWriteLock();
                        }
                    }
                    return true;
                }
                return false;
            }
            finally
            {
                if (_Lock.IsReadLockHeld)
                    _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Attempts to remove a key-value pair.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <param name="value">The removed value.</param>
        /// <returns>True if the key was removed.</returns>
        public override bool TryRemove(string key, out RedisValue value)
        {
            value = null;

            if (string.IsNullOrEmpty(key))
                return false;

            _Lock.EnterWriteLock();
            try
            {
                if (_Storage.TryGetValue(key, out value))
                {
                    return _Storage.Remove(key);
                }
                return false;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Attempts to update an existing value.
        /// </summary>
        /// <param name="key">The key to update.</param>
        /// <param name="newValue">The new value.</param>
        /// <param name="comparisonValue">The expected current value.</param>
        /// <returns>True if the value was updated.</returns>
        public override bool TryUpdate(string key, RedisValue newValue, RedisValue comparisonValue)
        {
            if (string.IsNullOrEmpty(key) || newValue == null)
                return false;

            _Lock.EnterWriteLock();
            try
            {
                RedisValue currentValue;
                if (_Storage.TryGetValue(key, out currentValue))
                {
                    // Check if current value matches comparison value
                    if (ReferenceEquals(currentValue, comparisonValue) ||
                        (currentValue != null && currentValue.Equals(comparisonValue)))
                    {
                        _Storage[key] = newValue;
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets all keys in the storage.
        /// </summary>
        /// <returns>Collection of all keys.</returns>
        public override IEnumerable<string> GetAllKeys()
        {
            _Lock.EnterReadLock();
            try
            {
                // Return a copy to avoid issues with concurrent modifications
                return _Storage.Keys.ToList();
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Clears all items from storage.
        /// </summary>
        public override void Clear()
        {
            _Lock.EnterWriteLock();
            try
            {
                _Storage.Clear();
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Checks if a key exists in storage.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists.</returns>
        protected override bool ContainsKeyInternal(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            _Lock.EnterReadLock();
            try
            {
                return _Storage.ContainsKey(key);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets or adds a value atomically.
        /// </summary>
        /// <param name="key">The key to get or add.</param>
        /// <param name="valueFactory">Factory function to create the value if key doesn't exist.</param>
        /// <returns>The existing or newly added value.</returns>
        public RedisValue GetOrAdd(string key, Func<string, RedisValue> valueFactory)
        {
            if (string.IsNullOrEmpty(key) || valueFactory == null)
                throw new ArgumentNullException();

            // Try to get with read lock first
            _Lock.EnterReadLock();
            try
            {
                RedisValue existingValue;
                if (_Storage.TryGetValue(key, out existingValue) && !existingValue.IsExpired)
                {
                    return existingValue;
                }
            }
            finally
            {
                _Lock.ExitReadLock();
            }

            // Need to add or replace expired value
            _Lock.EnterWriteLock();
            try
            {
                // Double-check after acquiring write lock
                RedisValue existingValue;
                if (_Storage.TryGetValue(key, out existingValue) && !existingValue.IsExpired)
                {
                    return existingValue;
                }

                RedisValue newValue = valueFactory(key);
                _Storage[key] = newValue;
                return newValue;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Adds or updates a value atomically.
        /// </summary>
        /// <param name="key">The key to add or update.</param>
        /// <param name="addValue">The value to add if key doesn't exist.</param>
        /// <param name="updateValueFactory">Factory function to update existing value.</param>
        /// <returns>The new value that was added or updated.</returns>
        public override RedisValue AddOrUpdate(string key, RedisValue addValue,
            Func<string, RedisValue, RedisValue> updateValueFactory)
        {
            if (string.IsNullOrEmpty(key) || addValue == null || updateValueFactory == null)
                throw new ArgumentNullException();

            _Lock.EnterWriteLock();
            try
            {
                RedisValue existingValue;
                if (_Storage.TryGetValue(key, out existingValue) && !existingValue.IsExpired)
                {
                    RedisValue updatedValue = updateValueFactory(key, existingValue);
                    _Storage[key] = updatedValue;
                    return updatedValue;
                }
                else
                {
                    _Storage[key] = addValue;
                    return addValue;
                }
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Disposes of the storage and releases the lock.
        /// </summary>
        public override void Dispose()
        {
            if (!_Disposed)
            {
                base.Dispose();
                _Lock?.Dispose();
                _Disposed = true;
            }
        }

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8601 // Possible null reference assignment.
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}