namespace Sample.RedisServer
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Entry point for the Sample Redis Server application.
    /// </summary>
    /// <remarks>
    /// This console application creates and starts a Redis-compatible server
    /// using the RedisRespServer library with in-memory storage.
    /// </remarks>
    public class Program
    {
        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        /// <param name="args">Command line arguments. First argument can specify the port (default: 6379).</param>
        /// <returns>A task representing the asynchronous program execution.</returns>
        /// <remarks>
        /// The application accepts an optional port number as the first command line argument.
        /// If no port is specified, it defaults to 6379 (standard Redis port).
        /// The server will run until the user presses any key to exit.
        /// </remarks>
        public static async Task Main(string[] args)
        {
            int port = 6379;

            if (args.Length > 0 && int.TryParse(args[0], out var customPort))
            {
                port = customPort;
            }

            Console.WriteLine($"Starting Sample Redis Server on port {port}");
            Console.WriteLine("Press any key to stop the server...");

            var server = new RedisServer(port);
            await server.StartAsync();

            try
            {
                Console.ReadKey();
            }
            catch (InvalidOperationException)
            {
                // Running in non-interactive mode, wait indefinitely
                Console.WriteLine("Running in non-interactive mode. Press Ctrl+C to stop.");
                await Task.Delay(Timeout.Infinite);
            }

            server.Stop();
            Console.WriteLine("Server stopped. Press any key to exit.");
            try
            {
                Console.ReadKey();
            }
            catch (InvalidOperationException)
            {
                // Ignore in non-interactive mode
            }
        }
    }
}
