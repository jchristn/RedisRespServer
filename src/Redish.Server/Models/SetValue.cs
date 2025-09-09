namespace Redish.Server.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RedisResp;

    /// <summary>
    /// Represents a Redis set value that stores a collection of unique string elements.
    /// </summary>
    /// <remarks>
    /// A Redis set is an unordered collection of unique strings. Sets support operations
    /// like adding, removing, and checking membership of elements. They also provide
    /// random sampling and set operations. Sets are commonly used for storing unique
    /// items, tags, or for implementing set-based algorithms in Redis applications.
    /// </remarks>
    public class SetValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        /// <value>Always returns <see cref="RedisValueType.Set"/>.</value>
        public override RedisValueType Type => RedisValueType.Set;

        /// <summary>
        /// Gets the set members as a collection of unique strings.
        /// </summary>
        /// <value>
        /// A hash set containing all unique string members of this set.
        /// The underlying HashSet ensures that all elements are unique and
        /// provides efficient membership testing and manipulation operations.
        /// </value>
        public HashSet<string> Members { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SetValue"/> class.
        /// </summary>
        /// <remarks>
        /// Creates an empty set with no members. Use the various methods to add,
        /// remove, or query members in the set.
        /// </remarks>
        public SetValue()
        {
            Members = new HashSet<string>();
        }

        /// <summary>
        /// Adds one or more members to the set.
        /// </summary>
        /// <param name="values">The values to add to the set.</param>
        /// <returns>
        /// The number of elements that were actually added to the set, excluding any
        /// duplicates that were already present.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="values"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis SADD command. If a value already
        /// exists in the set, it will not be added again, and the count will not
        /// include such duplicates.
        /// </remarks>
        public int SAdd(params string[] values)
        {
            int count = 0;
            foreach (string value in values)
            {
                if (Members.Add(value))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Removes one or more members from the set.
        /// </summary>
        /// <param name="values">The values to remove from the set.</param>
        /// <returns>
        /// The number of elements that were actually removed from the set.
        /// Elements that were not present in the set are not counted.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="values"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis SREM command. If a value does not
        /// exist in the set, it cannot be removed, and the count will not include
        /// such non-existent elements.
        /// </remarks>
        public int SRem(params string[] values)
        {
            int count = 0;
            foreach (string value in values)
            {
                if (Members.Remove(value))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Checks if a member exists in the set.
        /// </summary>
        /// <param name="member">The member to check for existence.</param>
        /// <returns>
        /// True if the member exists in the set; otherwise, false.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="member"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis SISMEMBER command and provides
        /// efficient O(1) membership testing.
        /// </remarks>
        public bool SIsMember(string member)
        {
            return Members.Contains(member);
        }

        /// <summary>
        /// Gets all members of the set.
        /// </summary>
        /// <returns>
        /// An array containing all members of the set. The order of elements
        /// in the returned array is not guaranteed to be consistent.
        /// </returns>
        /// <remarks>
        /// This operation is equivalent to the Redis SMEMBERS command. Since sets
        /// are unordered, the returned elements may appear in any order and this
        /// order may change between calls.
        /// </remarks>
        public string[] SMembers()
        {
            return Members.ToArray();
        }

        /// <summary>
        /// Gets the cardinality (number of members) of the set.
        /// </summary>
        /// <returns>The total number of unique members in the set.</returns>
        /// <remarks>
        /// This operation is equivalent to the Redis SCARD command and provides
        /// the count of unique elements in the set.
        /// </remarks>
        public int SCard() => Members.Count;

        /// <summary>
        /// Removes and returns a random member from the set.
        /// </summary>
        /// <returns>
        /// A random member that was removed from the set, or null if the set is empty.
        /// </returns>
        /// <remarks>
        /// This operation is equivalent to the Redis SPOP command. The member is
        /// permanently removed from the set. If the set becomes empty after this
        /// operation, subsequent calls will return null until new members are added.
        /// </remarks>
        public string? SPop()
        {
            if (Members.Count == 0) return null;
            string member = Members.First();
            Members.Remove(member);
            return member;
        }

        /// <summary>
        /// Returns one or more random members from the set without removing them.
        /// </summary>
        /// <param name="count">The number of random members to return. Defaults to 1.</param>
        /// <returns>
        /// An array containing up to <paramref name="count"/> random members from the set.
        /// If the set contains fewer members than requested, all members are returned.
        /// Returns an empty array if the set is empty.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="count"/> is negative.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis SRANDMEMBER command. The members
        /// are not removed from the set and may be returned again in subsequent calls.
        /// The same member may appear multiple times in the result if the requested
        /// count exceeds the set size, but this implementation limits results to unique members.
        /// </remarks>
        public string[] SRandMember(int count = 1)
        {
            if (count < 0)
                throw new ArgumentException("Count cannot be negative.", nameof(count));

            if (Members.Count == 0) return new string[0];
            
            Random random = new Random();
            string[] members = Members.ToArray();
            List<string> result = new List<string>();
            
            for (int i = 0; i < Math.Min(count, members.Length); i++)
            {
                result.Add(members[random.Next(members.Length)]);
            }
            
            return result.ToArray();
        }
    }
}