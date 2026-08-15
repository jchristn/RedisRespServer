namespace Test.Shared.Infrastructure
{
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Allocates free TCP ports for integration tests that need to bind a listener.
    /// </summary>
    /// <remarks>
    /// Using an ephemeral, OS-assigned port for each test avoids collisions with other
    /// processes (and with parallel test runs) that a fixed port such as 6379/6380 would cause.
    /// </remarks>
    public static class PortAllocator
    {

        #region Public-Members

        #endregion


        #region Private-Members

        private static readonly object _Lock = new object();

        #endregion


        #region Constructors-and-Factories

        #endregion


        #region Public-Methods

        /// <summary>
        /// Gets a currently-free TCP port on the loopback interface.
        /// </summary>
        /// <returns>A port number that was free at the moment of the call.</returns>
        /// <remarks>
        /// The port is discovered by binding a temporary listener to port 0 (which asks the OS
        /// for a free port) and then releasing it. There is a small race window between release
        /// and reuse, but it is acceptable for test scenarios.
        /// </remarks>
        public static int GetFreePort()
        {
            lock (_Lock)
            {
                TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
        }

        #endregion


        #region Private-Methods

        #endregion

    }
}
