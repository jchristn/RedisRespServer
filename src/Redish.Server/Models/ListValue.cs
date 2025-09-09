namespace Redish.Server.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RedisResp;

    /// <summary>
    /// Represents a Redis list value that stores an ordered collection of string elements.
    /// </summary>
    /// <remarks>
    /// A Redis list is a sequence of ordered elements, similar to a linked list.
    /// Elements can be added to either end of the list, and the list supports
    /// operations like push, pop, and range queries. Lists are commonly used for
    /// implementing queues, stacks, and timeline-like data structures in Redis.
    /// </remarks>
    public class ListValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        /// <value>Always returns <see cref="RedisValueType.List"/>.</value>
        public override RedisValueType Type => RedisValueType.List;

        /// <summary>
        /// Gets the list elements in their ordered sequence.
        /// </summary>
        /// <value>
        /// A list containing the string elements in the order they were added.
        /// The first element is at index 0, and the last element is at index Count-1.
        /// </value>
        public List<string> Elements { get; private set; }

        /// <summary>
        /// Gets the length (number of elements) of the list.
        /// </summary>
        /// <returns>The total number of elements currently in the list.</returns>
        /// <remarks>
        /// This operation is equivalent to the Redis LLEN command. The length is
        /// always a non-negative integer, with 0 indicating an empty list.
        /// </remarks>
        public int LLen() => Elements.Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListValue"/> class.
        /// </summary>
        /// <remarks>
        /// Creates an empty list with no elements. Use the push methods to add elements
        /// to either end of the list, or other methods to manipulate the list contents.
        /// </remarks>
        public ListValue()
        {
            Elements = new List<string>();
        }

        /// <summary>
        /// Pushes one or more elements to the right (end) of the list.
        /// </summary>
        /// <param name="values">The values to push to the end of the list.</param>
        /// <returns>The new length of the list after the push operation.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="values"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis RPUSH command. Elements are added
        /// in the order they appear in the values array, so the first value in the array
        /// will be inserted first, followed by subsequent values.
        /// </remarks>
        public int RPush(params string[] values)
        {
            Elements.AddRange(values);
            return Elements.Count;
        }

        /// <summary>
        /// Pushes one or more elements to the left (beginning) of the list.
        /// </summary>
        /// <param name="values">The values to push to the beginning of the list.</param>
        /// <returns>The new length of the list after the push operation.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="values"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis LPUSH command. Elements are inserted
        /// in reverse order of how they appear in the values array, so that after the
        /// operation, the elements appear in the same order as in the input array.
        /// </remarks>
        public int LPush(params string[] values)
        {
            Elements.InsertRange(0, values.Reverse());
            return Elements.Count;
        }

        /// <summary>
        /// Pops (removes and returns) an element from the right (end) of the list.
        /// </summary>
        /// <returns>
        /// The element that was removed from the end of the list, or null if the list is empty.
        /// </returns>
        /// <remarks>
        /// This operation is equivalent to the Redis RPOP command. If the list becomes
        /// empty after this operation, subsequent calls will return null until new
        /// elements are added to the list.
        /// </remarks>
        public string? RPop()
        {
            if (Elements.Count == 0) return null;
            string value = Elements[Elements.Count - 1];
            Elements.RemoveAt(Elements.Count - 1);
            return value;
        }

        /// <summary>
        /// Pops (removes and returns) an element from the left (beginning) of the list.
        /// </summary>
        /// <returns>
        /// The element that was removed from the beginning of the list, or null if the list is empty.
        /// </returns>
        /// <remarks>
        /// This operation is equivalent to the Redis LPOP command. If the list becomes
        /// empty after this operation, subsequent calls will return null until new
        /// elements are added to the list.
        /// </remarks>
        public string? LPop()
        {
            if (Elements.Count == 0) return null;
            string value = Elements[0];
            Elements.RemoveAt(0);
            return value;
        }

        /// <summary>
        /// Gets a range of elements from the list by their indices.
        /// </summary>
        /// <param name="start">The start index of the range (inclusive).</param>
        /// <param name="stop">The stop index of the range (inclusive).</param>
        /// <returns>
        /// An array containing the elements within the specified range, or an empty
        /// array if the range is invalid or the list is empty.
        /// </returns>
        /// <remarks>
        /// This operation is equivalent to the Redis LRANGE command. Both start and stop
        /// indices can be negative, where -1 refers to the last element, -2 to the
        /// second-to-last element, and so on. If start is greater than stop, an empty
        /// array is returned. Indices are automatically clamped to valid ranges.
        /// </remarks>
        public string[] LRange(int start, int stop)
        {
            if (Elements.Count == 0) return new string[0];
            
            // Handle negative indices
            if (start < 0) start = Elements.Count + start;
            if (stop < 0) stop = Elements.Count + stop;
            
            // Clamp to valid range
            start = Math.Max(0, Math.Min(start, Elements.Count - 1));
            stop = Math.Max(0, Math.Min(stop, Elements.Count - 1));
            
            if (start > stop) return new string[0];
            
            List<string> result = new List<string>();
            for (int i = start; i <= stop; i++)
            {
                result.Add(Elements[i]);
            }
            
            return result.ToArray();
        }
    }
}