namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the expiration and time-to-live behavior of the abstract <see cref="RedisValue"/> base class.
    /// </summary>
    public static class RedisValueSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "RedisValue";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the RedisValue test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "no-expiration-ttl", "TTL is -1 when no expiration is set", () =>
                {
                    TestRedisValue v = new TestRedisValue();
                    Check.Equal(-1, v.GetTtl());
                    Check.False(v.IsExpired);
                    Check.Null(v.ExpiresAt);
                }),

                Cases.Sync(SuiteId, "set-expiration-positive-ttl", "SetExpiration yields a positive TTL", () =>
                {
                    TestRedisValue v = new TestRedisValue();
                    v.SetExpiration(60);
                    Check.NotNull(v.ExpiresAt);
                    Check.False(v.IsExpired);
                    int ttl = v.GetTtl();
                    Check.True(ttl > 0 && ttl <= 60, "TTL should be within (0,60], was " + ttl);
                }),

                Cases.Sync(SuiteId, "expired-value-ttl", "TTL is -2 and IsExpired is true for a past expiration", () =>
                {
                    TestRedisValue v = new TestRedisValue();
                    v.ExpiresAt = DateTime.UtcNow.AddSeconds(-5);
                    Check.True(v.IsExpired);
                    Check.Equal(-2, v.GetTtl());
                }),

                Cases.Sync(SuiteId, "remove-expiration", "RemoveExpiration clears the expiration", () =>
                {
                    TestRedisValue v = new TestRedisValue();
                    v.SetExpiration(60);
                    v.RemoveExpiration();
                    Check.Null(v.ExpiresAt);
                    Check.Equal(-1, v.GetTtl());
                    Check.False(v.IsExpired);
                }),

                Cases.Sync(SuiteId, "set-expiration-zero", "SetExpiration(0) is allowed and marks the value expired", () =>
                {
                    TestRedisValue v = new TestRedisValue();
                    v.SetExpiration(0);
                    Check.NotNull(v.ExpiresAt);
                    // Zero seconds means it expires effectively immediately.
                    Check.True(v.GetTtl() <= 0);
                }),

                Cases.Sync(SuiteId, "set-expiration-negative-throws", "SetExpiration with a negative value throws", () =>
                {
                    TestRedisValue v = new TestRedisValue();
                    Check.Throws<ArgumentOutOfRangeException>(() => v.SetExpiration(-1));
                }),

                Cases.Sync(SuiteId, "type-property", "Concrete Type property is reported", () =>
                {
                    TestRedisValue v = new TestRedisValue();
                    Check.Equal(RedisValueType.String, v.Type);
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "RedisValue expiration and TTL",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
