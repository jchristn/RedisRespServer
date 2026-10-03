namespace Redish.Server
{
    using Redish.Server.Settings;
    using Redish.Server.Telemetry;
    using SerializationHelper;
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection.PortableExecutable;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Entry point for the Redish Server application.
    /// </summary>
    /// <remarks>
    /// This console application creates and starts a Redis-compatible server
    /// using the RedishServer class with configurable settings including
    /// console output, network binding, SSL, and syslog logging support.
    /// </remarks>
    public class Program
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        private static string _Header = "[Redish] ";
        private static Serializer _Serializer = new Serializer();
        private static ServerSettings _Settings = null;
        private static LoggingModule _Logging = null;
        private static RedishServer _Server = null;
        private static RedishTelemetryHost _Telemetry = null;
        private static CancellationTokenSource _TokenSource = new CancellationTokenSource();

        public static async Task Main(string[] args)
        {
            Welcome();
            InitializeSettings();
            InitializeGlobals();

            await _Server.StartAsync().ConfigureAwait(false);

            EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            AssemblyLoadContext.Default.Unloading += (ctx) => waitHandle.Set();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                _Logging.Info(_Header + "termination signal received");
                waitHandle.Set();
                eventArgs.Cancel = true;
            };

            bool waitHandleSignal = false;
            do
            {
                waitHandleSignal = waitHandle.WaitOne(1000);
            }
            while (!waitHandleSignal);

            _Logging.Info(_Header + "stopping at " + DateTime.UtcNow);

            try { _Server?.Dispose(); } catch (Exception e) { _Logging.Warn(_Header + "error stopping server: " + e.Message); }
            _Telemetry?.Dispose();
        }

        private static void Welcome()
        {
            Console.WriteLine();
            Console.WriteLine(Constants.AnsiArt);
            Console.WriteLine(Constants.ProductName);
            Console.WriteLine(Constants.Copyright);
            Console.WriteLine();
        }

        private static void InitializeSettings()
        {
            if (File.Exists(Constants.SettingsFile))
            {
                Console.WriteLine("Loading settings from " + Constants.SettingsFile);
                _Settings = _Serializer.DeserializeJson<ServerSettings>(File.ReadAllText(Constants.SettingsFile));
            }
            else
            {
                Console.WriteLine("Creating settings file " + Constants.SettingsFile);
                _Settings = new ServerSettings();
                File.WriteAllText(Constants.SettingsFile, _Serializer.SerializeJson(_Settings, true));
            }
        }

        private static void InitializeGlobals()
        {
            // Convert local SyslogServer to SyslogLogging.SyslogServer format
            List<SyslogLogging.SyslogServer> syslogServers = new List<SyslogLogging.SyslogServer>();
            foreach (Settings.SyslogServer server in _Settings.Logging.Servers)
            {
                syslogServers.Add(new SyslogLogging.SyslogServer(server.Hostname, server.Port));
            }
            
            _Logging = new LoggingModule(syslogServers);

            // Single telemetry host for the process; subscribes to the Redish.Server and RedisRespServer sources.
            _Telemetry = RedishTelemetryHost.Start(_Settings.Telemetry, _Logging);
            if (_Telemetry.IsActive)
            {
                _Logging.Info(_Header + "telemetry enabled, service " + _Settings.Telemetry.ServiceName
                    + (_Settings.Telemetry.OtlpEnabled ? ", OTLP " + _Settings.Telemetry.OtlpEndpoint : "")
                    + (_Telemetry.ScrapeUrl != null ? ", metrics " + _Telemetry.ScrapeUrl : ""));
            }
            else
            {
                _Logging.Info(_Header + "telemetry disabled");
            }

            _Server = new RedishServer(_Settings, _Logging);
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}