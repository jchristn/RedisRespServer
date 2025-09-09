namespace Sample.RedisInterface
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RedisResp;

    /// <summary>
    /// Represents a Redis set value.
    /// </summary>
    public class SetValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        public override RedisValueType Type => RedisValueType.Set;

        /// <summary>
        /// Gets the set members.
        /// </summary>
        /// <value>The set data as a hash set of strings.</value>
        public HashSet<string> Members { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SetValue"/> class.
        /// </summary>
        public SetValue()
        {
            Members = new HashSet<string>();
        }

        /// <summary>
        /// Adds members to the set.
        /// </summary>
        /// <param name="values">The values to add.</param>
        /// <returns>The number of elements that were added (excluding duplicates).</returns>
        public int SAdd(params string[] values)
        {
            int count = 0;
            foreach (var value in values)
            {
                if (Members.Add(value))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Removes members from the set.
        /// </summary>
        /// <param name="values">The values to remove.</param>
        /// <returns>The number of elements that were removed.</returns>
        public int SRem(params string[] values)
        {
            int count = 0;
            foreach (var value in values)
            {
                if (Members.Remove(value))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Checks if a member exists in the set.
        /// </summary>
        /// <param name="member">The member to check.</param>
        /// <returns>True if the member exists; otherwise, false.</returns>
        public bool SIsMember(string member)
        {
            return Members.Contains(member);
        }

        /// <summary>
        /// Gets all members of the set.
        /// </summary>
        /// <returns>An array of all members.</returns>
        public string[] SMembers()
        {
            return Members.ToArray();
        }

        /// <summary>
        /// Gets the cardinality (number of members) of the set.
        /// </summary>
        /// <returns>The number of members in the set.</returns>
        public int SCard() => Members.Count;

        /// <summary>
        /// Pops a random member from the set.
        /// </summary>
        /// <returns>The popped member, or null if the set is empty.</returns>
        public string? SPop()
        {
            if (Members.Count == 0) return null;
            var member = Members.First();
            Members.Remove(member);
            return member;
        }

        /// <summary>
        /// Returns random members from the set without removing them.
        /// </summary>
        /// <param name="count">The number of members to return.</param>
        /// <returns>An array of random members.</returns>
        public string[] SRandMember(int count = 1)
        {
            if (Members.Count == 0) return new string[0];
            
            var random = new Random();
            var members = Members.ToArray();
            var result = new List<string>();
            
            for (int i = 0; i < Math.Min(count, members.Length); i++)
            {
                result.Add(members[random.Next(members.Length)]);
            }
            
            return result.ToArray();
        }
    }
}