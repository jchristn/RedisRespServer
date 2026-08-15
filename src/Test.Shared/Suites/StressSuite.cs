namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// High-volume and large-payload stress tests for the listener's parsing pipeline.
    /// </summary>
    /// <remarks>
    /// These migrate the volume-oriented scenarios from the legacy test harness, adding real
    /// assertions (the legacy versions sent data without verifying anything).
    /// </remarks>
    public static class StressSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "Stress";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the stress test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Async(SuiteId, "high-volume-simple-strings", "100 simple strings in one write all parse", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < 100; i++) sb.Append("+Message").Append(i).Append("\r\n");
                        await client.SendAsync(sb.ToString());

                        bool got = await harness.WaitForCountAsync(100, Timeout);
                        Check.True(got, "expected all 100 messages to be parsed, saw " + harness.Data.Count);
                    }
                }),

                Cases.Async(SuiteId, "large-bulk-string", "A 10,000-byte bulk string parses intact", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        string payload = new string('A', 10000);
                        await client.SendAsync("$10000\r\n" + payload + "\r\n");

                        RespDataReceivedEventArgs e = await harness.WaitForAsync(
                            x => x.DataType == RespDataType.BulkString, Timeout);
                        Check.NotNull(e, "expected the large bulk string to parse");
                        Check.Equal(10000, ((string)e.Value).Length);
                        Check.Equal(payload, (string)e.Value);
                    }
                }),

                Cases.Async(SuiteId, "many-arrays-pipelined", "50 command arrays pipelined in one write all parse", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < 50; i++)
                        {
                            string key = "key" + i;
                            sb.Append("*2\r\n$3\r\nGET\r\n$").Append(key.Length).Append("\r\n").Append(key).Append("\r\n");
                        }
                        await client.SendAsync(sb.ToString());

                        bool got = await harness.WaitForCountAsync(50, Timeout);
                        Check.True(got, "expected all 50 arrays to be parsed, saw " + harness.Data.Count);
                    }
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Stress and volume",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
