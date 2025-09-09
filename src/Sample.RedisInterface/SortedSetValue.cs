namespace Sample.RedisInterface
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RedisResp;

    /// <summary>
    /// Represents a Redis sorted set value.
    /// </summary>
    public class SortedSetValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        public override RedisValueType Type => RedisValueType.SortedSet;

        /// <summary>
        /// Gets the sorted set members with scores.
        /// </summary>
        /// <value>The sorted set data as a dictionary of member to score.</value>
        public Dictionary<string, double> Members { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SortedSetValue"/> class.
        /// </summary>
        public SortedSetValue()
        {
            Members = new Dictionary<string, double>();
        }

        /// <summary>
        /// Adds members with scores to the sorted set.
        /// </summary>
        /// <param name="scoreMembers">Alternating score, member pairs.</param>
        /// <returns>The number of elements that were added (excluding updates).</returns>
        public int ZAdd(params object[] scoreMembers)
        {
            if (scoreMembers.Length % 2 != 0)
                throw new ArgumentException("Score-member pairs must be provided");
                
            int count = 0;
            for (int i = 0; i < scoreMembers.Length; i += 2)
            {
                if (double.TryParse(scoreMembers[i].ToString(), out double score))
                {
                    var member = scoreMembers[i + 1].ToString() ?? "";
                    if (!Members.ContainsKey(member))
                        count++;
                    Members[member] = score;
                }
            }
            return count;
        }

        /// <summary>
        /// Removes members from the sorted set.
        /// </summary>
        /// <param name="members">The members to remove.</param>
        /// <returns>The number of elements that were removed.</returns>
        public int ZRem(params string[] members)
        {
            int count = 0;
            foreach (var member in members)
            {
                if (Members.Remove(member))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Gets the score of a member.
        /// </summary>
        /// <param name="member">The member.</param>
        /// <returns>The score, or null if the member doesn't exist.</returns>
        public double? ZScore(string member)
        {
            return Members.TryGetValue(member, out var score) ? score : null;
        }

        /// <summary>
        /// Gets the cardinality (number of members) of the sorted set.
        /// </summary>
        /// <returns>The number of members in the sorted set.</returns>
        public int ZCard() => Members.Count;

        /// <summary>
        /// Gets a range of members by rank (index).
        /// </summary>
        /// <param name="start">The start index.</param>
        /// <param name="stop">The stop index.</param>
        /// <param name="withScores">Whether to include scores in the result.</param>
        /// <returns>An array of members, optionally with scores.</returns>
        public string[] ZRange(int start, int stop, bool withScores = false)
        {
            var sorted = Members.OrderBy(kvp => kvp.Value).ToList();
            
            if (sorted.Count == 0) return new string[0];
            
            // Handle negative indices
            if (start < 0) start = sorted.Count + start;
            if (stop < 0) stop = sorted.Count + stop;
            
            // Clamp to valid range
            start = Math.Max(0, Math.Min(start, sorted.Count - 1));
            stop = Math.Max(0, Math.Min(stop, sorted.Count - 1));
            
            if (start > stop) return new string[0];
            
            var result = new List<string>();
            for (int i = start; i <= stop; i++)
            {
                result.Add(sorted[i].Key);
                if (withScores)
                    result.Add(sorted[i].Value.ToString());
            }
            
            return result.ToArray();
        }

        /// <summary>
        /// Increments the score of a member.
        /// </summary>
        /// <param name="increment">The increment value.</param>
        /// <param name="member">The member.</param>
        /// <returns>The new score.</returns>
        public double ZIncrBy(double increment, string member)
        {
            var newScore = (Members.TryGetValue(member, out var currentScore) ? currentScore : 0) + increment;
            Members[member] = newScore;
            return newScore;
        }
    }
}