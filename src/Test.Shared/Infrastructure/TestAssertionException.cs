namespace Test.Shared.Infrastructure
{
    using System;

    /// <summary>
    /// Exception thrown when a Touchstone test assertion fails.
    /// </summary>
    /// <remarks>
    /// Touchstone treats any exception thrown from a test case delegate as a failure.
    /// This dedicated type makes assertion failures easy to distinguish from unexpected
    /// runtime exceptions in test output and stack traces.
    /// </remarks>
    public class TestAssertionException : Exception
    {

        #region Public-Members

        #endregion


        #region Private-Members

        #endregion


        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="TestAssertionException"/> class.
        /// </summary>
        /// <param name="message">The assertion failure message.</param>
        public TestAssertionException(string message) : base(message)
        {
        }

        #endregion


        #region Public-Methods

        #endregion


        #region Private-Methods

        #endregion

    }
}
