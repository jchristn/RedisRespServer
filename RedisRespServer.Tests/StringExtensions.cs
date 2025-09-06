using System.Text;

namespace RedisResp.Tests
{
    /// <summary>
    /// Provides extension methods for string manipulation used in testing scenarios.
    /// </summary>
    /// <remarks>
    /// This static class contains utility methods that extend the string class
    /// to provide additional functionality needed for test formatting and display.
    /// </remarks>
    public static class StringExtensions
    {
        /// <summary>
        /// Repeats a string a specified number of times.
        /// </summary>
        /// <param name="source">The string to repeat.</param>
        /// <param name="count">The number of times to repeat the string.</param>
        /// <returns>A new string containing the source string repeated count times.</returns>
        /// <remarks>
        /// This method efficiently concatenates the source string multiple times using
        /// a StringBuilder for optimal performance. Useful for creating separator lines
        /// or repeated patterns in console output.
        /// </remarks>
        /// <example>
        /// <code>
        /// string separator = "=".Multiply(50); // Creates "=================================================="
        /// string padding = " ".Multiply(10);   // Creates "          "
        /// </code>
        /// </example>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when count is negative.</exception>
        public static string Multiply(this string source, int count)
        {
            if (count < 0)
                throw new System.ArgumentOutOfRangeException(nameof(count), "Count cannot be negative");

            if (count == 0 || string.IsNullOrEmpty(source))
                return string.Empty;

            return new StringBuilder(source.Length * count)
                .Insert(0, source, count)
                .ToString();
        }
    }
}