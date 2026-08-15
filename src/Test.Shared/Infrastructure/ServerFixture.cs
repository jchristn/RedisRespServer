namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Redish.Server;
    using Redish.Server.Settings;
    using SyslogLogging;

    /// <summary>
    /// Boots an in-process <see cref="RedishServer"/> on an ephemeral port for end-to-end command tests
    /// and provides helpers to connect clients and exchange RESP commands.
    /// </summary>
    /// <remarks>
    /// A quiet logging module (console disabled, a single unreachable UDP syslog target) is used so
    /// the test output is not polluted by server logs. The server is fully self-contained and requires
    /// no external Redis instance.
    /// </remarks>
    public sealed class ServerFixture : IDisposable
    {

        #region Public-Members

        /// <summary>
        /// Gets the port the server is listening on.
        /// </summary>
        /// <value>The TCP port number.</value>
        public int Port
        {
            get { return _Port; }
        }

        /// <summary>
        /// Gets the underlying server instance.
        /// </summary>
        /// <value>The <see cref="RedishServer"/> under test.</value>
        public RedishServer Server
        {
            get { return _Server; }
        }

        #endregion


        #region Private-Members

        private readonly int _Port;
        private readonly RedishServer _Server;
        private readonly LoggingModule _Logging;

        #endregion


        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerFixture"/> class.
        /// </summary>
        /// <param name="port">The port the server was started on.</param>
        /// <param name="server">The server instance.</param>
        /// <param name="logging">The logging module owned by the fixture.</param>
        private ServerFixture(int port, RedishServer server, LoggingModule logging)
        {
            _Port = port;
            _Server = server;
            _Logging = logging;
        }

        /// <summary>
        /// Starts a new in-process server on a free port.
        /// </summary>
        /// <returns>A started fixture.</returns>
        public static async Task<ServerFixture> StartAsync()
        {
            int port = PortAllocator.GetFreePort();
            LoggingModule logging = BuildQuietLogging();
            ServerSettings settings = new ServerSettings
            {
                Port = port,
                EnableConsole = false
            };

            RedishServer server = new RedishServer(settings, logging);
            await server.StartAsync().ConfigureAwait(false);
            return new ServerFixture(port, server, logging);
        }

        #endregion


        #region Public-Methods

        /// <summary>
        /// Connects a new raw client to the server.
        /// </summary>
        /// <returns>A connected client owned by the caller.</returns>
        public async Task<RespRawClient> ConnectAsync()
        {
            RespRawClient client = new RespRawClient();
            await client.ConnectAsync("127.0.0.1", _Port).ConfigureAwait(false);
            return client;
        }

        /// <summary>
        /// Sends a command as a RESP array of bulk strings and returns the raw response.
        /// </summary>
        /// <param name="client">The connected client.</param>
        /// <param name="args">The command and its arguments.</param>
        /// <returns>The raw RESP response.</returns>
        public static async Task<string> SendCommandAsync(RespRawClient client, params string[] args)
        {
            await client.SendAsync(BuildCommand(args)).ConfigureAwait(false);
            return await client.ReadResponseAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }

        /// <summary>
        /// Builds a RESP array-of-bulk-strings command frame.
        /// </summary>
        /// <param name="args">The command tokens.</param>
        /// <returns>The encoded RESP command.</returns>
        public static string BuildCommand(params string[] args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('*').Append(args.Length).Append("\r\n");
            foreach (string arg in args)
            {
                int len = Encoding.Latin1.GetByteCount(arg);
                sb.Append('$').Append(len).Append("\r\n").Append(arg).Append("\r\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Stops the server and releases all resources.
        /// </summary>
        public void Dispose()
        {
            try { _Server.Dispose(); } catch { }
            try { _Logging?.Dispose(); } catch { }
        }

        #endregion


        #region Private-Methods

        /// <summary>
        /// Builds a logging module that does not write to the console.
        /// </summary>
        /// <returns>A logging module suitable for tests.</returns>
        private static LoggingModule BuildQuietLogging()
        {
            try
            {
                LoggingModule module = new LoggingModule(new List<SyslogLogging.SyslogServer>());
                try { module.Settings.EnableConsole = false; } catch { }
                return module;
            }
            catch
            {
                return new LoggingModule("127.0.0.1", 514, false);
            }
        }

        #endregion

    }
}
