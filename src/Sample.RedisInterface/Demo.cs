namespace Sample.RedisInterface
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Demonstrates the RespInterface authentication functionality and Name property.
    /// </summary>
    /// <remarks>
    /// This class shows how to use the RespInterface.Authenticate function to control
    /// client authentication and how the Name property works with CLIENT SETNAME.
    /// </remarks>
    public static class Demo
    {
        /// <summary>
        /// Demonstrates authentication functionality with custom authentication logic.
        /// </summary>
        /// <param name="args">Command line arguments. First argument can specify the port (default: 6380).</param>
        /// <returns>A task representing the asynchronous demo execution.</returns>
        /// <remarks>
        /// This demo sets up a server with custom authentication that only allows
        /// username "admin" with password "secret123" or password-only "guest".
        /// It also demonstrates how client names are tracked via CLIENT SETNAME.
        /// </remarks>
        public static async Task RunAuthenticationDemo(string[] args)
        {
            int port = 6380;
            if (args.Length > 0 && int.TryParse(args[0], out var customPort))
            {
                port = customPort;
            }

            Console.WriteLine("=== RespInterface Authentication Demo ===");
            Console.WriteLine($"Starting Redis Interface Server with authentication on port {port}");
            Console.WriteLine("Authentication rules:");
            Console.WriteLine("  - Username 'admin' with password 'secret123' -> ALLOWED");
            Console.WriteLine("  - Password-only 'guest' -> ALLOWED");
            Console.WriteLine("  - All other attempts -> DENIED");
            Console.WriteLine();

            var server = new RedisInterfaceServer(port);

            // Set up custom authentication logic
            server.RespInterface.Authenticate = (username, password) =>
            {
                if (username == null && password == "guest")
                {
                    Console.WriteLine($"[AUTH] Password-only authentication: '{password}' -> ALLOWED");
                    return true;
                }
                else if (username == "admin" && password == "secret123")
                {
                    Console.WriteLine($"[AUTH] User authentication: '{username}' with password -> ALLOWED");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[AUTH] Authentication failed: user='{username ?? "null"}', password='{password}' -> DENIED");
                    return false;
                }
            };

            // Demonstrate client name tracking
            server.RespInterface.ClientConnectedAction = (e) =>
            {
                Console.WriteLine($"[CLIENT] New connection: {e.GUID} from {e.RemoteEndPoint}");
            };

            server.RespInterface.ClientDisconnectedAction = (e) =>
            {
                // Try to get client info to show the name if it was set
                var clientInfo = server.RespInterface.Listener.RetrieveClientByGuid(e.GUID);
                var clientName = clientInfo?.Name ?? "unnamed";
                Console.WriteLine($"[CLIENT] Disconnected: {e.GUID} (name: {clientName}) - {e.Reason}");
            };

            await server.StartAsync();

            Console.WriteLine("Server ready! Test authentication with redis-cli:");
            Console.WriteLine($"  redis-cli -p {port} AUTH guest");
            Console.WriteLine($"  redis-cli -p {port} AUTH admin secret123");
            Console.WriteLine($"  redis-cli -p {port} AUTH wrong password");
            Console.WriteLine();
            Console.WriteLine("Test client naming:");
            Console.WriteLine($"  redis-cli -p {port} CLIENT SETNAME my-client-name");
            Console.WriteLine();
            Console.WriteLine("Press any key to stop the server...");

            try
            {
                Console.ReadKey();
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Running in non-interactive mode. Press Ctrl+C to stop.");
                await Task.Delay(System.Threading.Timeout.Infinite);
            }

            server.Stop();
            Console.WriteLine("Demo completed.");
        }
    }
}