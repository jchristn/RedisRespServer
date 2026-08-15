namespace Test.Shared.Infrastructure
{
    using RedisResp;

    /// <summary>
    /// A minimal concrete <see cref="RedisValue"/> used to exercise the abstract base class'
    /// expiration and time-to-live behavior in isolation.
    /// </summary>
    /// <remarks>
    /// <see cref="RedisValue"/> is abstract, so a concrete subclass is required to test the
    /// expiration logic implemented on the base type without pulling in the server's model types.
    /// </remarks>
    public sealed class TestRedisValue : RedisValue
    {

        #region Public-Members

        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        /// <value>Always returns <see cref="RedisValueType.String"/> for test purposes.</value>
        public override RedisValueType Type
        {
            get { return RedisValueType.String; }
        }

        #endregion


        #region Private-Members

        #endregion


        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="TestRedisValue"/> class.
        /// </summary>
        public TestRedisValue()
        {
        }

        #endregion


        #region Public-Methods

        #endregion


        #region Private-Methods

        #endregion

    }
}
