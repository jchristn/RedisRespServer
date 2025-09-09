namespace RedisResp
{
    using System;

    /// <summary>
    /// Base class for all Redis value types.
    /// </summary>
    public abstract class RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        public abstract RedisValueType Type { get; }

        /// <summary>
        /// Gets or sets the expiration time for this value.
        /// </summary>
        /// <value>The expiration time in UTC, or null if no expiration is set.</value>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets a value indicating whether this value has expired.
        /// </summary>
        /// <value>True if the value has expired; otherwise, false.</value>
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

        /// <summary>
        /// Gets the Time To Live (TTL) for this value in seconds.
        /// </summary>
        /// <returns>The TTL in seconds, -1 if no expiration is set, or -2 if expired.</returns>
        public int GetTtl()
        {
            if (!ExpiresAt.HasValue)
                return -1;
            
            if (IsExpired)
                return -2;
            
            var remainingTime = ExpiresAt.Value - DateTime.UtcNow;
            return Math.Max(0, (int)remainingTime.TotalSeconds);
        }

        /// <summary>
        /// Sets the expiration time for this value.
        /// </summary>
        /// <param name="seconds">The number of seconds until expiration.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when seconds is negative.</exception>
        public void SetExpiration(int seconds)
        {
            if (seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Expiration time cannot be negative");
            
            ExpiresAt = DateTime.UtcNow.AddSeconds(seconds);
        }

        /// <summary>
        /// Removes the expiration time from this value.
        /// </summary>
        public void RemoveExpiration()
        {
            ExpiresAt = null;
        }
    }
}