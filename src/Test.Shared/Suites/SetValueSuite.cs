namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Redish.Server.Models;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the <see cref="SetValue"/> model type and its membership operations.
    /// </summary>
    public static class SetValueSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "SetValue";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the SetValue test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "type", "SetValue reports the Set type", () =>
                {
                    Check.Equal(RedisValueType.Set, new SetValue().Type);
                }),

                Cases.Sync(SuiteId, "sadd-counts-new", "SAdd returns the number of newly added members", () =>
                {
                    SetValue s = new SetValue();
                    Check.Equal(3, s.SAdd("a", "b", "c"));
                    Check.Equal(3, s.SCard());
                }),

                Cases.Sync(SuiteId, "sadd-ignores-duplicates", "SAdd does not count duplicates already present", () =>
                {
                    SetValue s = new SetValue();
                    s.SAdd("a", "b");
                    Check.Equal(1, s.SAdd("b", "c"));
                    Check.Equal(3, s.SCard());
                }),

                Cases.Sync(SuiteId, "sismember", "SIsMember reflects membership", () =>
                {
                    SetValue s = new SetValue();
                    s.SAdd("a");
                    Check.True(s.SIsMember("a"));
                    Check.False(s.SIsMember("z"));
                }),

                Cases.Sync(SuiteId, "srem", "SRem removes members and counts removals", () =>
                {
                    SetValue s = new SetValue();
                    s.SAdd("a", "b", "c");
                    Check.Equal(2, s.SRem("a", "b", "missing"));
                    Check.Equal(1, s.SCard());
                }),

                Cases.Sync(SuiteId, "smembers", "SMembers returns all unique members", () =>
                {
                    SetValue s = new SetValue();
                    s.SAdd("a", "b", "c");
                    string[] members = s.SMembers();
                    Check.Equal(3, members.Length);
                    Check.True(members.OrderBy(x => x).SequenceEqual(new[] { "a", "b", "c" }));
                }),

                Cases.Sync(SuiteId, "spop", "SPop removes and returns a member, shrinking the set", () =>
                {
                    SetValue s = new SetValue();
                    s.SAdd("only");
                    Check.Equal("only", s.SPop());
                    Check.Equal(0, s.SCard());
                }),

                Cases.Sync(SuiteId, "spop-empty-null", "SPop on an empty set returns null", () =>
                {
                    Check.Null(new SetValue().SPop());
                }),

                Cases.Sync(SuiteId, "srandmember", "SRandMember returns up to count existing members", () =>
                {
                    SetValue s = new SetValue();
                    s.SAdd("a", "b", "c");
                    string[] r = s.SRandMember(2);
                    Check.Equal(2, r.Length);
                    foreach (string m in r) Check.True(s.SIsMember(m));
                    Check.Equal(3, s.SCard(), "SRandMember must not remove members");
                }),

                Cases.Sync(SuiteId, "srandmember-empty", "SRandMember on an empty set returns an empty array", () =>
                {
                    Check.Equal(0, new SetValue().SRandMember(3).Length);
                }),

                Cases.Sync(SuiteId, "srandmember-negative-throws", "SRandMember with a negative count throws", () =>
                {
                    SetValue s = new SetValue();
                    s.SAdd("a");
                    Check.Throws<ArgumentException>(() => s.SRandMember(-1));
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "SetValue model",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
