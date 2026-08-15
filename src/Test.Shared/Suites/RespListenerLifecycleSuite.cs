namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the <see cref="RespListener"/> lifecycle, construction validation, and client management API.
    /// </summary>
    public static class RespListenerLifecycleSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "RespListenerLifecycle";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the listener lifecycle test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "ctor-negative-port-throws", "Constructing with a negative port throws", () =>
                {
                    Check.Throws<ArgumentOutOfRangeException>(() => new RespListener(-1));
                }),

                Cases.Sync(SuiteId, "ctor-large-port-throws", "Constructing with a port above 65535 throws", () =>
                {
                    Check.Throws<ArgumentOutOfRangeException>(() => new RespListener(70000));
                }),

                Cases.Sync(SuiteId, "not-listening-initially", "A newly constructed listener is not listening", () =>
                {
                    using (RespListener l = new RespListener(PortAllocator.GetFreePort()))
                    {
                        Check.False(l.IsListening);
                        Check.Equal(0, l.ConnectedClientsCount);
                        Check.Equal(0, l.RetrieveClients().Length);
                    }
                }),

                Cases.Async(SuiteId, "start-sets-listening", "StartAsync transitions the listener to the listening state", async ct =>
                {
                    using (RespListener l = new RespListener(PortAllocator.GetFreePort()))
                    {
                        await l.StartAsync();
                        Check.True(l.IsListening);
                        l.Stop();
                        Check.False(l.IsListening);
                    }
                }),

                Cases.Async(SuiteId, "double-start-is-noop", "Calling StartAsync twice does not throw", async ct =>
                {
                    using (RespListener l = new RespListener(PortAllocator.GetFreePort()))
                    {
                        await l.StartAsync();
                        await l.StartAsync();
                        Check.True(l.IsListening);
                        l.Stop();
                    }
                }),

                Cases.Sync(SuiteId, "stop-without-start-is-noop", "Stopping a listener that never started does not throw", () =>
                {
                    using (RespListener l = new RespListener(PortAllocator.GetFreePort()))
                    {
                        l.Stop();
                        Check.False(l.IsListening);
                    }
                }),

                Cases.Async(SuiteId, "dispose-stops", "Dispose stops a running listener", async ct =>
                {
                    RespListener l = new RespListener(PortAllocator.GetFreePort());
                    await l.StartAsync();
                    l.Dispose();
                    Check.False(l.IsListening);
                }),

                Cases.Async(SuiteId, "logger-invoked-on-start", "The optional logger receives a message when the listener starts", async ct =>
                {
                    List<string> messages = new List<string>();
                    using (RespListener l = new RespListener(PortAllocator.GetFreePort()))
                    {
                        l.Logger = (sev, msg) => { lock (messages) { messages.Add(msg); } };
                        await l.StartAsync();
                        l.Stop();
                    }
                    lock (messages)
                    {
                        Check.True(messages.Count > 0, "expected at least one log message");
                    }
                }),

                Cases.Sync(SuiteId, "retrieve-unknown-client-null", "RetrieveClientByGuid returns null for an unknown client", () =>
                {
                    using (RespListener l = new RespListener(PortAllocator.GetFreePort()))
                    {
                        Check.Null(l.RetrieveClientByGuid(Guid.NewGuid()));
                    }
                }),

                Cases.Sync(SuiteId, "disconnect-empty-guid-throws", "DisconnectClientByGuid with an empty GUID throws", () =>
                {
                    using (RespListener l = new RespListener(PortAllocator.GetFreePort()))
                    {
                        Check.Throws<ArgumentException>(() => l.DisconnectClientByGuid(Guid.Empty));
                    }
                }),

                Cases.Sync(SuiteId, "disconnect-unknown-guid-false", "DisconnectClientByGuid returns false for an unknown client", () =>
                {
                    using (RespListener l = new RespListener(PortAllocator.GetFreePort()))
                    {
                        Check.False(l.DisconnectClientByGuid(Guid.NewGuid()));
                    }
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "RespListener lifecycle",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
