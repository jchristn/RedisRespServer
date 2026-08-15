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
    /// Tests the higher-level <see cref="RespInterface"/> wrapper, including its functional
    /// handlers, action handlers, re-exposed events, and construction validation.
    /// </summary>
    public static class RespInterfaceSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "RespInterface";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the RespInterface test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "null-listener-throws", "Constructing with a null listener throws", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new RespInterface(null));
                }),

                Cases.Sync(SuiteId, "listener-property", "The Listener property exposes the wrapped listener", () =>
                {
                    using (RespListener l = new RespListener(PortAllocator.GetFreePort()))
                    using (RespInterface iface = new RespInterface(l))
                    {
                        Check.True(ReferenceEquals(l, iface.Listener));
                    }
                }),

                Cases.Sync(SuiteId, "authenticate-default-null", "The Authenticate delegate defaults to null (open access)", () =>
                {
                    using (RespListener l = new RespListener(PortAllocator.GetFreePort()))
                    using (RespInterface iface = new RespInterface(l))
                    {
                        Check.Null(iface.Authenticate);
                    }
                }),

                Cases.Async(SuiteId, "func-handler-invoked", "A functional handler receives parsed messages", async ct =>
                {
                    int port = PortAllocator.GetFreePort();
                    RespListener listener = new RespListener(port);
                    using (RespInterface iface = new RespInterface(listener))
                    {
                        string captured = null;
                        SemaphoreSlim signal = new SemaphoreSlim(0);
                        iface.SimpleStringHandler = e => { captured = (string)e.Value; signal.Release(); return null; };

                        await listener.StartAsync();
                        using (RespRawClient client = new RespRawClient())
                        {
                            await client.ConnectAsync("127.0.0.1", port);
                            await client.SendAsync("+HELLO\r\n");
                            Check.True(await signal.WaitAsync(Timeout), "the functional handler should be invoked");
                            Check.Equal("HELLO", captured);
                        }
                    }
                }),

                Cases.Async(SuiteId, "action-and-eventhandler-invoked", "Action and EventHandler handlers both fire for a message", async ct =>
                {
                    int port = PortAllocator.GetFreePort();
                    RespListener listener = new RespListener(port);
                    using (RespInterface iface = new RespInterface(listener))
                    {
                        int hits = 0;
                        SemaphoreSlim signal = new SemaphoreSlim(0);
                        iface.DoubleAction = e => { Interlocked.Increment(ref hits); signal.Release(); };
                        iface.DoubleEventHandler = (s, e) => { Interlocked.Increment(ref hits); signal.Release(); };

                        await listener.StartAsync();
                        using (RespRawClient client = new RespRawClient())
                        {
                            await client.ConnectAsync("127.0.0.1", port);
                            await client.SendAsync(",10\r\n");
                            Check.True(await signal.WaitAsync(Timeout), "at least one double handler should fire");
                            await Task.Delay(100);
                            Check.Equal(2, Volatile.Read(ref hits));
                        }
                    }
                }),

                Cases.Async(SuiteId, "reexposed-event-fires", "Events re-exposed on the interface fire for parsed messages", async ct =>
                {
                    int port = PortAllocator.GetFreePort();
                    RespListener listener = new RespListener(port);
                    using (RespInterface iface = new RespInterface(listener))
                    {
                        SemaphoreSlim signal = new SemaphoreSlim(0);
                        iface.IntegerReceived += (s, e) => signal.Release();

                        await listener.StartAsync();
                        using (RespRawClient client = new RespRawClient())
                        {
                            await client.ConnectAsync("127.0.0.1", port);
                            await client.SendAsync(":123\r\n");
                            Check.True(await signal.WaitAsync(Timeout), "the re-exposed IntegerReceived event should fire");
                        }
                    }
                }),

                Cases.Async(SuiteId, "dispose-stops-listener", "Disposing the interface disposes the underlying listener", async ct =>
                {
                    int port = PortAllocator.GetFreePort();
                    RespListener listener = new RespListener(port);
                    RespInterface iface = new RespInterface(listener);
                    await listener.StartAsync();
                    Check.True(listener.IsListening);
                    iface.Dispose();
                    Check.False(listener.IsListening, "disposing the interface should stop the listener");
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "RespInterface wrapper",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
