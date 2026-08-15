namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using RedisResp;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Tests for the <see cref="ClientInfo"/> value holder and its constructor validation.
    /// </summary>
    public static class ClientInfoSuite
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private const string SuiteId = "ClientInfo";

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Builds the ClientInfo test suite.
        /// </summary>
        /// <returns>A configured <see cref="TestSuiteDescriptor"/>.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Cases.Sync(SuiteId, "construct-valid", "Constructing with valid arguments populates all properties", () =>
                {
                    Guid g = Guid.NewGuid();
                    IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 12345);
                    using (TcpClient tcp = new TcpClient())
                    {
                        ClientInfo info = new ClientInfo(g, tcp, ep);
                        Check.Equal(g, info.GUID);
                        Check.Equal(ep, info.RemoteEndPoint);
                        Check.True(ReferenceEquals(tcp, info.TcpClient));
                        Check.True(info.ConnectedUtc <= DateTime.UtcNow.AddSeconds(1));
                        Check.Null(info.Name);
                    }
                }),

                Cases.Sync(SuiteId, "null-tcpclient-throws", "Constructing with a null TcpClient throws ArgumentNullException", () =>
                {
                    IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 12345);
                    Check.Throws<ArgumentNullException>(() => new ClientInfo(Guid.NewGuid(), null, ep));
                }),

                Cases.Sync(SuiteId, "null-endpoint-throws", "Constructing with a null endpoint throws ArgumentNullException", () =>
                {
                    using (TcpClient tcp = new TcpClient())
                    {
                        Check.Throws<ArgumentNullException>(() => new ClientInfo(Guid.NewGuid(), tcp, null));
                    }
                }),

                Cases.Sync(SuiteId, "name-settable", "The client name is settable (CLIENT SETNAME support)", () =>
                {
                    IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 12345);
                    using (TcpClient tcp = new TcpClient())
                    {
                        ClientInfo info = new ClientInfo(Guid.NewGuid(), tcp, ep) { Name = "worker-1" };
                        Check.Equal("worker-1", info.Name);
                    }
                })
            };

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "ClientInfo construction",
                cases: cases);
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
