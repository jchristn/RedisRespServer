namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests connection lifecycle events, connected-client accounting, and disconnect resilience.
    /// </summary>
    public static class ClientConnectionSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "ClientConnection";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the client connection test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Async(SuiteId, "connect-raises-event", "Connecting a client raises ClientConnected and increments the count", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        Check.True(await harness.WaitForConnectedCountAsync(1, Timeout), "count should reach 1");
                        Check.True(harness.ConnectedEvents >= 1, "a ClientConnected event should have fired");
                    }
                }),

                Cases.Async(SuiteId, "multiple-clients", "Multiple concurrent clients are all counted", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    {
                        RespRawClient a = await harness.ConnectClientAsync();
                        RespRawClient b = await harness.ConnectClientAsync();
                        RespRawClient c = await harness.ConnectClientAsync();
                        try
                        {
                            Check.True(await harness.WaitForConnectedCountAsync(3, Timeout), "count should reach 3");
                        }
                        finally
                        {
                            a.Dispose();
                            b.Dispose();
                            c.Dispose();
                        }
                    }
                }),

                Cases.Async(SuiteId, "graceful-disconnect", "A graceful disconnect decrements the count and raises ClientDisconnected", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    {
                        RespRawClient client = await harness.ConnectClientAsync();
                        Check.True(await harness.WaitForConnectedCountAsync(1, Timeout));
                        client.Dispose();
                        Check.True(await harness.WaitForConnectedCountAsync(0, Timeout), "count should return to 0");
                        Check.True(await harness.WaitForDisconnectedEventsAsync(1, Timeout), "a disconnect event should fire");
                    }
                }),

                Cases.Async(SuiteId, "sudden-disconnect-resilient", "An abrupt disconnect is handled and the server stays responsive", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    {
                        RespRawClient crasher = await harness.ConnectClientAsync();
                        Check.True(await harness.WaitForConnectedCountAsync(1, Timeout));
                        await crasher.SendAsync("$100\r\npartial");
                        crasher.HardClose();
                        Check.True(await harness.WaitForConnectedCountAsync(0, Timeout), "server should clean up the crashed client");

                        using (RespRawClient good = await harness.ConnectClientAsync())
                        {
                            await good.SendAsync("+OK\r\n");
                            RespDataReceivedEventArgs e = await harness.WaitForAsync(
                                x => x.DataType == RespDataType.SimpleString, Timeout);
                            Check.NotNull(e, "server should remain responsive after an abrupt disconnect");
                        }
                    }
                }),

                Cases.Async(SuiteId, "retrieve-clients", "RetrieveClients and RetrieveClientByGuid expose the connected client", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        Check.True(await harness.WaitForConnectedCountAsync(1, Timeout));
                        ClientInfo[] clients = harness.Listener.RetrieveClients();
                        Check.Equal(1, clients.Length);
                        Check.NotEqual(Guid.Empty, clients[0].GUID);
                        Check.NotNull(harness.Listener.RetrieveClientByGuid(clients[0].GUID));
                    }
                }),

                Cases.Async(SuiteId, "disconnect-by-guid", "DisconnectClientByGuid forcibly disconnects a known client", async ct =>
                {
                    using (ListenerHarness harness = await ListenerHarness.StartAsync())
                    using (RespRawClient client = await harness.ConnectClientAsync())
                    {
                        Check.True(await harness.WaitForConnectedCountAsync(1, Timeout));
                        ClientInfo[] clients = harness.Listener.RetrieveClients();
                        Check.True(harness.Listener.DisconnectClientByGuid(clients[0].GUID));
                        Check.True(await harness.WaitForConnectedCountAsync(0, Timeout), "the client should be removed");
                    }
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Client connection lifecycle",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
