namespace Sample.RedisInterface
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RedisResp;

    /// <summary>
    /// Represents a Redis list value.
    /// </summary>
    public class ListValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        public override RedisValueType Type => RedisValueType.List;

        /// <summary>
        /// Gets the list elements.
        /// </summary>
        /// <value>The list data as a list of strings.</value>
        public List<string> Elements { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ListValue"/> class.
        /// </summary>
        public ListValue()
        {
            Elements = new List<string>();
        }

        /// <summary>
        /// Pushes elements to the right (end) of the list.
        /// </summary>
        /// <param name="values">The values to push.</param>
        /// <returns>The new length of the list.</returns>
        public int RPush(params string[] values)
        {
            Elements.AddRange(values);
            return Elements.Count;
        }

        /// <summary>
        /// Pushes elements to the left (beginning) of the list.
        /// </summary>
        /// <param name="values">The values to push.</param>
        /// <returns>The new length of the list.</returns>
        public int LPush(params string[] values)
        {
            Elements.InsertRange(0, values.Reverse());
            return Elements.Count;
        }

        /// <summary>
        /// Pops an element from the right (end) of the list.
        /// </summary>
        /// <returns>The popped element, or null if the list is empty.</returns>
        public string? RPop()
        {
            if (Elements.Count == 0) return null;
            var value = Elements[Elements.Count - 1];
            Elements.RemoveAt(Elements.Count - 1);
            return value;
        }

        /// <summary>
        /// Pops an element from the left (beginning) of the list.
        /// </summary>
        /// <returns>The popped element, or null if the list is empty.</returns>
        public string? LPop()
        {
            if (Elements.Count == 0) return null;
            var value = Elements[0];
            Elements.RemoveAt(0);
            return value;
        }

        /// <summary>
        /// Gets a range of elements from the list.
        /// </summary>
        /// <param name="start">The start index.</param>
        /// <param name="stop">The stop index.</param>
        /// <returns>The range of elements.</returns>
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
            
            var result = new List<string>();
            for (int i = start; i <= stop; i++)
            {
                result.Add(Elements[i]);
            }
            
            return result.ToArray();
        }

        /// <summary>
        /// Gets the length of the list.
        /// </summary>
        /// <returns>The number of elements in the list.</returns>
        public int LLen() => Elements.Count;
    }
}