namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Lightweight, framework-agnostic assertion helpers used by Touchstone test descriptors.
    /// </summary>
    /// <remarks>
    /// These helpers deliberately avoid any dependency on xUnit, NUnit, or MSTest so that the
    /// shared test library remains the single source of truth that can run under any host.
    /// Each helper throws a <see cref="TestAssertionException"/> when the condition is not met.
    /// </remarks>
    public static class Check
    {

        #region Public-Members

        #endregion


        #region Private-Members

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Asserts that a condition is true.
        /// </summary>
        /// <param name="condition">The condition that must be true.</param>
        /// <param name="message">The message to report if the condition is false.</param>
        /// <exception cref="TestAssertionException">Thrown when the condition is false.</exception>
        public static void True(bool condition, string message = null)
        {
            if (!condition)
                throw new TestAssertionException(message ?? "Expected condition to be true but it was false.");
        }

        /// <summary>
        /// Asserts that a condition is false.
        /// </summary>
        /// <param name="condition">The condition that must be false.</param>
        /// <param name="message">The message to report if the condition is true.</param>
        /// <exception cref="TestAssertionException">Thrown when the condition is true.</exception>
        public static void False(bool condition, string message = null)
        {
            if (condition)
                throw new TestAssertionException(message ?? "Expected condition to be false but it was true.");
        }

        /// <summary>
        /// Asserts that two values are equal using the default equality comparer.
        /// </summary>
        /// <typeparam name="T">The type of the values.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="message">Optional context appended to the failure message.</param>
        /// <exception cref="TestAssertionException">Thrown when the values are not equal.</exception>
        public static void Equal<T>(T expected, T actual, string message = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                string ctx = message == null ? "" : " (" + message + ")";
                throw new TestAssertionException(
                    "Expected [" + Describe(expected) + "] but got [" + Describe(actual) + "]" + ctx + ".");
            }
        }

        /// <summary>
        /// Asserts that two values are not equal using the default equality comparer.
        /// </summary>
        /// <typeparam name="T">The type of the values.</typeparam>
        /// <param name="notExpected">The value the actual value must not equal.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="message">Optional context appended to the failure message.</param>
        /// <exception cref="TestAssertionException">Thrown when the values are equal.</exception>
        public static void NotEqual<T>(T notExpected, T actual, string message = null)
        {
            if (EqualityComparer<T>.Default.Equals(notExpected, actual))
            {
                string ctx = message == null ? "" : " (" + message + ")";
                throw new TestAssertionException(
                    "Expected value to differ from [" + Describe(actual) + "]" + ctx + ".");
            }
        }

        /// <summary>
        /// Asserts that a value is null.
        /// </summary>
        /// <param name="value">The value that must be null.</param>
        /// <param name="message">Optional context appended to the failure message.</param>
        /// <exception cref="TestAssertionException">Thrown when the value is not null.</exception>
        public static void Null(object value, string message = null)
        {
            if (value != null)
            {
                string ctx = message == null ? "" : " (" + message + ")";
                throw new TestAssertionException("Expected null but got [" + Describe(value) + "]" + ctx + ".");
            }
        }

        /// <summary>
        /// Asserts that a value is not null.
        /// </summary>
        /// <param name="value">The value that must not be null.</param>
        /// <param name="message">Optional context appended to the failure message.</param>
        /// <exception cref="TestAssertionException">Thrown when the value is null.</exception>
        public static void NotNull(object value, string message = null)
        {
            if (value == null)
            {
                string ctx = message == null ? "" : " (" + message + ")";
                throw new TestAssertionException("Expected a non-null value but got null" + ctx + ".");
            }
        }

        /// <summary>
        /// Asserts that two sequences contain equal elements in the same order.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="expected">The expected sequence.</param>
        /// <param name="actual">The actual sequence.</param>
        /// <param name="message">Optional context appended to the failure message.</param>
        /// <exception cref="TestAssertionException">Thrown when the sequences differ.</exception>
        public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message = null)
        {
            List<T> e = expected == null ? new List<T>() : expected.ToList();
            List<T> a = actual == null ? new List<T>() : actual.ToList();
            string ctx = message == null ? "" : " (" + message + ")";

            if (e.Count != a.Count)
                throw new TestAssertionException(
                    "Expected sequence of length " + e.Count + " but got length " + a.Count + ctx + ".");

            for (int i = 0; i < e.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(e[i], a[i]))
                    throw new TestAssertionException(
                        "Sequences differ at index " + i + ": expected [" + Describe(e[i]) +
                        "] but got [" + Describe(a[i]) + "]" + ctx + ".");
            }
        }

        /// <summary>
        /// Asserts that a collection contains a specific item.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="item">The item that must be present.</param>
        /// <param name="collection">The collection to search.</param>
        /// <param name="message">Optional context appended to the failure message.</param>
        /// <exception cref="TestAssertionException">Thrown when the item is not present.</exception>
        public static void Contains<T>(T item, IEnumerable<T> collection, string message = null)
        {
            if (collection == null || !collection.Contains(item))
            {
                string ctx = message == null ? "" : " (" + message + ")";
                throw new TestAssertionException("Expected collection to contain [" + Describe(item) + "]" + ctx + ".");
            }
        }

        /// <summary>
        /// Asserts that a string contains a specified substring.
        /// </summary>
        /// <param name="substring">The substring that must be present.</param>
        /// <param name="value">The string to search.</param>
        /// <param name="message">Optional context appended to the failure message.</param>
        /// <exception cref="TestAssertionException">Thrown when the substring is not present.</exception>
        public static void StringContains(string substring, string value, string message = null)
        {
            if (value == null || !value.Contains(substring))
            {
                string ctx = message == null ? "" : " (" + message + ")";
                throw new TestAssertionException(
                    "Expected string to contain [" + substring + "] but got [" + Describe(value) + "]" + ctx + ".");
            }
        }

        /// <summary>
        /// Asserts that invoking the supplied action throws an exception of the specified type.
        /// </summary>
        /// <typeparam name="TException">The expected exception type.</typeparam>
        /// <param name="action">The action expected to throw.</param>
        /// <param name="message">Optional context appended to the failure message.</param>
        /// <returns>The exception that was thrown.</returns>
        /// <exception cref="TestAssertionException">Thrown when no exception, or the wrong type, is thrown.</exception>
        public static TException Throws<TException>(Action action, string message = null) where TException : Exception
        {
            string ctx = message == null ? "" : " (" + message + ")";
            try
            {
                action();
            }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new TestAssertionException(
                    "Expected exception of type " + typeof(TException).Name + " but got " +
                    ex.GetType().Name + ": " + ex.Message + ctx + ".");
            }

            throw new TestAssertionException(
                "Expected exception of type " + typeof(TException).Name + " but none was thrown" + ctx + ".");
        }

        /// <summary>
        /// Asserts that awaiting the supplied asynchronous action throws an exception of the specified type.
        /// </summary>
        /// <typeparam name="TException">The expected exception type.</typeparam>
        /// <param name="action">The asynchronous action expected to throw.</param>
        /// <param name="message">Optional context appended to the failure message.</param>
        /// <returns>A task producing the exception that was thrown.</returns>
        /// <exception cref="TestAssertionException">Thrown when no exception, or the wrong type, is thrown.</exception>
        public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string message = null)
            where TException : Exception
        {
            string ctx = message == null ? "" : " (" + message + ")";
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new TestAssertionException(
                    "Expected exception of type " + typeof(TException).Name + " but got " +
                    ex.GetType().Name + ": " + ex.Message + ctx + ".");
            }

            throw new TestAssertionException(
                "Expected exception of type " + typeof(TException).Name + " but none was thrown" + ctx + ".");
        }

        /// <summary>
        /// Fails unconditionally with the supplied message.
        /// </summary>
        /// <param name="message">The failure message.</param>
        /// <exception cref="TestAssertionException">Always thrown.</exception>
        public static void Fail(string message)
        {
            throw new TestAssertionException(message);
        }

        #endregion


        #region Private-Methods

        /// <summary>
        /// Produces a human-readable description of a value for failure messages.
        /// </summary>
        /// <param name="value">The value to describe.</param>
        /// <returns>A readable string representation.</returns>
        private static string Describe(object value)
        {
            if (value == null) return "null";
            if (value is string s) return "\"" + s + "\"";

            if (value is IEnumerable enumerable && !(value is string))
            {
                List<string> parts = new List<string>();
                foreach (object item in enumerable)
                    parts.Add(item == null ? "null" : item.ToString());
                return "[" + string.Join(", ", parts) + "]";
            }

            return value.ToString();
        }

        #endregion

    }
}
