namespace Redish.Server.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RedisResp;

    /// <summary>
    /// Represents a Redis sorted set value that stores members with associated numeric scores.
    /// </summary>
    /// <remarks>
    /// A Redis sorted set is a collection of unique string members, each associated with a
    /// floating-point score. Members are ordered by their scores in ascending order, with
    /// lexicographic ordering used as a tiebreaker for members with identical scores.
    /// Sorted sets are commonly used for implementing leaderboards, priority queues,
    /// and range-based queries in Redis applications.
    /// </remarks>
    public class SortedSetValue : RedisValue
    {
        /// <summary>
        /// Gets the type of this Redis value.
        /// </summary>
        /// <value>Always returns <see cref="RedisValueType.SortedSet"/>.</value>
        public override RedisValueType Type => RedisValueType.SortedSet;

        /// <summary>
        /// Gets the sorted set members with their associated scores.
        /// </summary>
        /// <value>
        /// A dictionary mapping each unique member string to its associated numeric score.
        /// The key represents the member name and the value represents its score used for ordering.
        /// </value>
        public Dictionary<string, double> Members { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SortedSetValue"/> class.
        /// </summary>
        /// <remarks>
        /// Creates an empty sorted set with no members. Use the various methods to add,
        /// remove, or query members and their scores in the sorted set.
        /// </remarks>
        public SortedSetValue()
        {
            Members = new Dictionary<string, double>();
        }

        /// <summary>
        /// Adds one or more members with scores to the sorted set.
        /// </summary>
        /// <param name="scoreMembers">
        /// An array containing alternating score and member values. Must contain an even
        /// number of elements where even indices contain scores (convertible to double)
        /// and odd indices contain member names (convertible to string).
        /// </param>
        /// <returns>
        /// The number of new members that were added to the set. Updates to existing
        /// members' scores are not counted.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="scoreMembers"/> does not contain an even number of elements.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="scoreMembers"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis ZADD command. If a member already
        /// exists in the sorted set, its score is updated to the new value, but this
        /// update is not counted in the return value.
        /// </remarks>
        public int ZAdd(params object[] scoreMembers)
        {
            if (scoreMembers == null)
                throw new ArgumentNullException(nameof(scoreMembers));

            if (scoreMembers.Length % 2 != 0)
                throw new ArgumentException("Score-member pairs must be provided", nameof(scoreMembers));
                
            int count = 0;
            for (int i = 0; i < scoreMembers.Length; i += 2)
            {
                if (double.TryParse(scoreMembers[i].ToString(), out double score))
                {
                    string member = scoreMembers[i + 1].ToString() ?? "";
                    if (!Members.ContainsKey(member))
                        count++;
                    Members[member] = score;
                }
            }
            return count;
        }

        /// <summary>
        /// Removes one or more members from the sorted set.
        /// </summary>
        /// <param name="members">The member names to remove from the sorted set.</param>
        /// <returns>
        /// The number of members that were actually removed. Members that were not
        /// present in the sorted set are not counted.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="members"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis ZREM command. If a member does not
        /// exist in the sorted set, it cannot be removed, and the count will not include
        /// such non-existent members.
        /// </remarks>
        public int ZRem(params string[] members)
        {
            if (members == null)
                throw new ArgumentNullException(nameof(members));

            int count = 0;
            foreach (string member in members)
            {
                if (Members.Remove(member))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Gets the score of a specific member in the sorted set.
        /// </summary>
        /// <param name="member">The member name to query.</param>
        /// <returns>
        /// The score of the member if it exists in the sorted set; otherwise, null.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="member"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis ZSCORE command and provides
        /// efficient lookup of a member's associated score.
        /// </remarks>
        public double? ZScore(string member)
        {
            if (member == null)
                throw new ArgumentNullException(nameof(member));

            return Members.TryGetValue(member, out double score) ? score : null;
        }

        /// <summary>
        /// Gets the cardinality (number of members) of the sorted set.
        /// </summary>
        /// <returns>The total number of members in the sorted set.</returns>
        /// <remarks>
        /// This operation is equivalent to the Redis ZCARD command and provides
        /// the count of unique members in the sorted set.
        /// </remarks>
        public int ZCard() => Members.Count;

        /// <summary>
        /// Gets a range of members from the sorted set by their rank (position).
        /// </summary>
        /// <param name="start">The starting rank (0-based index) of the range.</param>
        /// <param name="stop">The ending rank (0-based index) of the range, inclusive.</param>
        /// <param name="withScores">
        /// If true, includes scores in the result array alternating with member names.
        /// If false, returns only member names.
        /// </param>
        /// <returns>
        /// An array containing the members in the specified range, ordered by score.
        /// If <paramref name="withScores"/> is true, the array contains alternating
        /// member names and scores. Returns an empty array if the range is invalid.
        /// </returns>
        /// <remarks>
        /// This operation is equivalent to the Redis ZRANGE command. Both start and stop
        /// indices can be negative, where -1 refers to the highest-scored member, -2 to the
        /// second-highest-scored member, and so on. Members are returned in ascending order
        /// by score. If start is greater than stop, an empty array is returned.
        /// </remarks>
        public string[] ZRange(int start, int stop, bool withScores = false)
        {
            List<KeyValuePair<string, double>> sorted = Members.OrderBy(kvp => kvp.Value).ToList();
            
            if (sorted.Count == 0) return new string[0];
            
            // Handle negative indices
            if (start < 0) start = sorted.Count + start;
            if (stop < 0) stop = sorted.Count + stop;
            
            // Clamp to valid range
            start = Math.Max(0, Math.Min(start, sorted.Count - 1));
            stop = Math.Max(0, Math.Min(stop, sorted.Count - 1));
            
            if (start > stop) return new string[0];
            
            List<string> result = new List<string>();
            for (int i = start; i <= stop; i++)
            {
                result.Add(sorted[i].Key);
                if (withScores)
                    result.Add(sorted[i].Value.ToString());
            }
            
            return result.ToArray();
        }

        /// <summary>
        /// Increments the score of a member by the specified amount.
        /// </summary>
        /// <param name="increment">The amount to add to the member's current score.</param>
        /// <param name="member">The member whose score should be incremented.</param>
        /// <returns>
        /// The new score of the member after the increment operation.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="member"/> is null.
        /// </exception>
        /// <remarks>
        /// This operation is equivalent to the Redis ZINCRBY command. If the member does not
        /// exist in the sorted set, it is created with the increment value as its initial score.
        /// The increment can be negative to decrease the score.
        /// </remarks>
        public double ZIncrBy(double increment, string member)
        {
            if (member == null)
                throw new ArgumentNullException(nameof(member));

            double newScore = (Members.TryGetValue(member, out double currentScore) ? currentScore : 0) + increment;
            Members[member] = newScore;
            return newScore;
        }
    }
}