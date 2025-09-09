namespace Redish.Server.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using RedisResp;
    using Redish.Server.Models;

    /// <summary>
    /// Abstract base class for Redis storage implementations.
    /// </summary>
    /// <remarks>
    /// Provides a contract for thread-safe storage with automatic expiration handling,
    /// type-safe operations, and common Redis functionality.
    /// </remarks>
    public abstract class StorageBase : IDisposable
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8603 // Possible null reference return.

        private readonly Timer _ExpirationTimer;
        private volatile bool _Disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageBase"/> class.
        /// </summary>
        protected StorageBase()
        {
            // Start background timer for cleaning up expired keys
            _ExpirationTimer = new Timer(
                _ => CleanupExpiredKeysAsync().ConfigureAwait(false),
                null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1)
            );
        }

        /// <summary>
        /// Gets or sets a value with automatic expiration checking.
        /// </summary>
        /// <param name="key">The key of the value.</param>
        /// <returns>The value if it exists and hasn't expired; otherwise, null.</returns>
        public abstract RedisValue this[string key] { get; set; }

        /// <summary>
        /// Attempts to add a new key-value pair.
        /// </summary>
        /// <param name="key">The key to add.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>True if the key was added; false if it already exists.</returns>
        public abstract bool TryAdd(string key, RedisValue value);

        /// <summary>
        /// Attempts to get a value by key.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The value if found.</param>
        /// <returns>True if the key exists and hasn't expired.</returns>
        public abstract bool TryGetValue(string key, out RedisValue value);

        /// <summary>
        /// Attempts to remove a key-value pair.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <param name="value">The removed value.</param>
        /// <returns>True if the key was removed.</returns>
        public abstract bool TryRemove(string key, out RedisValue value);

        /// <summary>
        /// Attempts to update an existing value.
        /// </summary>
        /// <param name="key">The key to update.</param>
        /// <param name="newValue">The new value.</param>
        /// <param name="comparisonValue">The expected current value.</param>
        /// <returns>True if the value was updated.</returns>
        public abstract bool TryUpdate(string key, RedisValue newValue, RedisValue comparisonValue);

        /// <summary>
        /// Gets all keys in the storage.
        /// </summary>
        /// <returns>Collection of all keys.</returns>
        public abstract IEnumerable<string> GetAllKeys();

        /// <summary>
        /// Gets the count of items in storage.
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// Clears all items from storage.
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// Checks if a key exists in storage.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists.</returns>
        protected abstract bool ContainsKeyInternal(string key);

        /// <summary>
        /// Attempts to get a value of a specific type.
        /// </summary>
        /// <typeparam name="T">The expected type of the Redis value.</typeparam>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The value if found and of correct type.</param>
        /// <returns>True if the value exists, hasn't expired, and is of the correct type.</returns>
        public virtual bool TryGetValue<T>(string key, out T value) where T : RedisValue
        {
            value = null;
            RedisValue redisValue;

            if (TryGetValue(key, out redisValue))
            {
                if (redisValue.IsExpired)
                {
                    RedisValue removedValue;
                    TryRemove(key, out removedValue);
                    return false;
                }

                // Check if the value is of the requested type
                if (redisValue is T)
                {
                    value = (T)redisValue;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all keys that haven't expired.
        /// </summary>
        /// <returns>Collection of active keys.</returns>
        public virtual IEnumerable<string> GetActiveKeys()
        {
            List<string> keys = GetAllKeys().ToList();
            foreach (string key in keys)
            {
                RedisValue value;
                if (TryGetValue(key, out value) && !value.IsExpired)
                {
                    yield return key;
                }
            }
        }

        /// <summary>
        /// Gets all keys matching a pattern that haven't expired.
        /// </summary>
        /// <param name="pattern">The pattern to match (supports * and ? wildcards).</param>
        /// <returns>Collection of matching active keys.</returns>
        public virtual IEnumerable<string> GetKeysByPattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return GetActiveKeys();

            string regex = ConvertPatternToRegex(pattern);

            return GetActiveKeys()
                .Where(key => System.Text.RegularExpressions.Regex.IsMatch(key, regex));
        }

        /// <summary>
        /// Checks if a key exists and hasn't expired.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists and hasn't expired.</returns>
        public virtual bool Exists(string key)
        {
            RedisValue value;
            if (TryGetValue(key, out value))
            {
                if (value.IsExpired)
                {
                    RedisValue removedValue;
                    TryRemove(key, out removedValue);
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Counts keys that exist and haven't expired.
        /// </summary>
        /// <param name="keys">The keys to check.</param>
        /// <returns>The number of existing, non-expired keys.</returns>
        public virtual int Exists(params string[] keys)
        {
            if (keys == null || keys.Length == 0)
                return 0;

            return keys.Count(Exists);
        }

        /// <summary>
        /// Gets the type of value stored at a key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>The Redis value type, or null if key doesn't exist or is expired.</returns>
        public virtual RedisValueType? GetType(string key)
        {
            RedisValue value;
            if (TryGetValue(key, out value))
            {
                if (value.IsExpired)
                {
                    RedisValue removedValue;
                    TryRemove(key, out removedValue);
                    return null;
                }
                return value.Type;
            }
            return null;
        }

        /// <summary>
        /// Sets the expiration time for a key.
        /// </summary>
        /// <param name="key">The key to set expiration for.</param>
        /// <param name="seconds">The number of seconds until expiration.</param>
        /// <returns>True if the expiration was set; false if key doesn't exist.</returns>
        public virtual bool Expire(string key, int seconds)
        {
            RedisValue value;
            if (TryGetValue(key, out value))
            {
                if (value.IsExpired)
                {
                    RedisValue removedValue;
                    TryRemove(key, out removedValue);
                    return false;
                }

                value.SetExpiration(seconds);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets the expiration time for a key using a specific DateTime.
        /// </summary>
        /// <param name="key">The key to set expiration for.</param>
        /// <param name="expireAt">The DateTime when the key should expire.</param>
        /// <returns>True if the expiration was set; false if key doesn't exist.</returns>
        public virtual bool ExpireAt(string key, DateTime expireAt)
        {
            RedisValue value;
            if (TryGetValue(key, out value))
            {
                if (value.IsExpired)
                {
                    RedisValue removedValue;
                    TryRemove(key, out removedValue);
                    return false;
                }

                value.ExpiresAt = expireAt;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes expiration from a key, making it persistent.
        /// </summary>
        /// <param name="key">The key to persist.</param>
        /// <returns>True if the key was made persistent; false if key doesn't exist.</returns>
        public virtual bool Persist(string key)
        {
            RedisValue value;
            if (TryGetValue(key, out value))
            {
                if (value.IsExpired)
                {
                    RedisValue removedValue;
                    TryRemove(key, out removedValue);
                    return false;
                }

                value.RemoveExpiration();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the Time To Live for a key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>TTL in seconds, -1 if no expiration, -2 if key doesn't exist or expired.</returns>
        public virtual int Ttl(string key)
        {
            RedisValue value;
            if (TryGetValue(key, out value))
            {
                if (value.IsExpired)
                {
                    RedisValue removedValue;
                    TryRemove(key, out removedValue);
                    return -2;
                }
                return value.GetTtl();
            }
            return -2;
        }

        /// <summary>
        /// Deletes one or more keys.
        /// </summary>
        /// <param name="keys">The keys to delete.</param>
        /// <returns>The number of keys that were deleted.</returns>
        public virtual int Del(params string[] keys)
        {
            if (keys == null || keys.Length == 0)
                return 0;

            int count = 0;
            foreach (string key in keys)
            {
                RedisValue removedValue;
                if (TryRemove(key, out removedValue))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Renames a key.
        /// </summary>
        /// <param name="oldKey">The current key name.</param>
        /// <param name="newKey">The new key name.</param>
        /// <returns>True if the key was renamed; false if oldKey doesn't exist.</returns>
        public virtual bool Rename(string oldKey, string newKey)
        {
            if (string.IsNullOrEmpty(oldKey) || string.IsNullOrEmpty(newKey))
                return false;

            RedisValue value;
            if (TryGetValue(oldKey, out value))
            {
                if (value.IsExpired)
                {
                    RedisValue removedValue;
                    TryRemove(oldKey, out removedValue);
                    return false;
                }

                // Remove any existing value at newKey
                RedisValue existingValue;
                TryRemove(newKey, out existingValue);

                // Add with new key
                if (TryAdd(newKey, value))
                {
                    RedisValue removedValue;
                    TryRemove(oldKey, out removedValue);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Renames a key only if the new key doesn't exist.
        /// </summary>
        /// <param name="oldKey">The current key name.</param>
        /// <param name="newKey">The new key name.</param>
        /// <returns>True if the key was renamed; false if oldKey doesn't exist or newKey exists.</returns>
        public virtual bool RenameNx(string oldKey, string newKey)
        {
            if (string.IsNullOrEmpty(oldKey) || string.IsNullOrEmpty(newKey))
                return false;

            if (Exists(newKey))
                return false;

            return Rename(oldKey, newKey);
        }

        /// <summary>
        /// Gets a random key from the storage.
        /// </summary>
        /// <returns>A random key, or null if storage is empty.</returns>
        public virtual string RandomKey()
        {
            List<string> activeKeys = GetActiveKeys().ToList();
            if (activeKeys.Count == 0)
                return null;

            Random random = new Random();
            return activeKeys[random.Next(activeKeys.Count)];
        }

        /// <summary>
        /// Adds or updates a value atomically.
        /// </summary>
        /// <param name="key">The key to add or update.</param>
        /// <param name="addValue">The value to add if key doesn't exist.</param>
        /// <param name="updateValueFactory">Factory function to update existing value.</param>
        /// <returns>The new value that was added or updated.</returns>
        public virtual RedisValue AddOrUpdate(string key, RedisValue addValue,
            Func<string, RedisValue, RedisValue> updateValueFactory)
        {
            if (string.IsNullOrEmpty(key) || addValue == null || updateValueFactory == null)
                throw new ArgumentNullException();

            RedisValue existingValue;
            if (TryGetValue(key, out existingValue) && !existingValue.IsExpired)
            {
                RedisValue updatedValue = updateValueFactory(key, existingValue);
                this[key] = updatedValue;
                return updatedValue;
            }
            else
            {
                this[key] = addValue;
                return addValue;
            }
        }

        /// <summary>
        /// Converts a Redis pattern to a regex pattern.
        /// </summary>
        /// <param name="pattern">The Redis pattern with * and ? wildcards.</param>
        /// <returns>The equivalent regex pattern.</returns>
        /// <remarks>
        /// Redis supports glob-style patterns where:
        /// - * matches any sequence of characters (including empty)
        /// - ? matches exactly one character
        /// - [abc] matches one character from the set
        /// - [a-z] matches one character from the range
        /// 
        /// Examples:
        /// - "user:*" matches "user:123", "user:abc", etc.
        /// - "user:?" matches "user:1", "user:a" but not "user:12"
        /// - "user:[0-9]*" matches "user:123", "user:456", etc.
        /// </remarks>
        protected virtual string ConvertPatternToRegex(string pattern)
        {
            // Escape special regex characters except for our wildcards
            string escaped = System.Text.RegularExpressions.Regex.Escape(pattern);

            // Convert Redis wildcards to regex equivalents
            // \* becomes .* (any characters)
            escaped = escaped.Replace("\\*", ".*");

            // \? becomes . (single character)
            escaped = escaped.Replace("\\?", ".");

            // Add anchors to match entire string
            return "^" + escaped + "$";
        }

        /// <summary>
        /// Cleans up expired keys from storage.
        /// </summary>
        protected virtual async Task CleanupExpiredKeysAsync()
        {
            if (_Disposed)
                return;

            await Task.Run(() =>
            {
                List<string> keys = GetAllKeys().ToList();
                foreach (string key in keys)
                {
                    RedisValue value;
                    if (TryGetValue(key, out value) && value.IsExpired)
                    {
                        RedisValue removedValue;
                        TryRemove(key, out removedValue);
                    }
                }
            });
        }

        /// <summary>
        /// Disposes of the storage and stops the expiration timer.
        /// </summary>
        public virtual void Dispose()
        {
            if (!_Disposed)
            {
                _Disposed = true;
                _ExpirationTimer?.Dispose();
                Clear();
            }
        }

#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}