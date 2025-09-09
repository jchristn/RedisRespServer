namespace Sample.RedisClient
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Entry point for the Sample Redis Client application.
    /// </summary>
    /// <remarks>
    /// This console application provides an interactive Redis client that can connect
    /// to any Redis server and execute persistence-related commands.
    /// </remarks>
    public class Program
    {
        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        /// <param name="args">Command line arguments. Hostname and port are required.</param>
        /// <returns>A task representing the asynchronous program execution.</returns>
        /// <remarks>
        /// The application requires hostname and port as command line arguments.
        /// Usage: Sample.RedisClient.exe &lt;hostname&gt; &lt;port&gt;
        /// Example: Sample.RedisClient.exe localhost 6379
        /// </remarks>
        public static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Usage();
                return;
            }

            string host = args[0];
            
            if (!int.TryParse(args[1], out int port) || port <= 0 || port > 65535)
            {
                Console.WriteLine("Error: Port must be a valid number between 1 and 65535.");
                Usage();
                return;
            }

            Console.WriteLine("=== Sample Redis Client ===");
            Console.WriteLine($"Connecting to Redis server at {host}:{port}");
            Console.WriteLine();

            var client = new RedisClient(host, port);
            await RunInteractiveSessionAsync(client, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Displays usage information for the application.
        /// </summary>
        /// <remarks>
        /// Shows the correct command line syntax and provides usage examples.
        /// </remarks>
        public static void Usage()
        {
            Console.WriteLine("Sample Redis Client");
            Console.WriteLine("Usage: Sample.RedisClient.exe <hostname> <port>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  hostname    The Redis server hostname or IP address");
            Console.WriteLine("  port        The Redis server port number (1-65535)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  Sample.RedisClient.exe localhost 6379");
            Console.WriteLine("  Sample.RedisClient.exe 192.168.1.100 6380");
            Console.WriteLine("  Sample.RedisClient.exe redis.example.com 6379");
        }

        private static async Task RunInteractiveSessionAsync(RedisClient client, CancellationToken cancellationToken = default)
        {
            try
            {
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                
                Console.WriteLine("Connected successfully!");
                Console.WriteLine();

                // Automatically run the StackExchange.Redis sequence
                await client.RunStackExchangeRedisSequenceAsync(cancellationToken).ConfigureAwait(false);

                Console.WriteLine();
                Console.WriteLine("StackExchange.Redis sequence completed. Starting interactive mode...");
                Console.WriteLine();
                ShowCommandMenu();

                while (!cancellationToken.IsCancellationRequested)
                {
                    Console.Write("Command [?/help]: ");
                    var input = Console.ReadLine()?.Trim();
                    
                    if (string.IsNullOrEmpty(input)) continue;
                    
                    if (input.Equals("quit", StringComparison.OrdinalIgnoreCase) || 
                        input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (input.Equals("?"))
                    {
                        ShowCommandMenu();
                        continue;
                    }

                    if (input.Equals("stackexchange", StringComparison.OrdinalIgnoreCase) ||
                        input.Equals("se", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Running StackExchange.Redis sequence again...");
                        try
                        {
                            await client.RunStackExchangeRedisSequenceAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error running StackExchange.Redis sequence: {ex.Message}");
                        }
                        continue;
                    }

                    try
                    {
                        await ProcessCommandAsync(client, input, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error executing command: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
            }
            finally
            {
                await client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static void ShowCommandMenu()
        {
            Console.WriteLine();
            Console.WriteLine("=== Redis Command Menu ===");
            Console.WriteLine();
            Console.WriteLine("Basic Commands:");
            Console.WriteLine("  SET key value          Set a key to a value, e.g. SET mykey \"hello world\"");
            Console.WriteLine("  GET key                Get the value of a key, e.g. GET mykey");
            Console.WriteLine();
            Console.WriteLine("Other Commands:");
            Console.WriteLine("  DEL key                Delete a key, e.g. DEL mykey");
            Console.WriteLine("  EXISTS key             Check if a key exists, e.g. EXISTS mykey");
            Console.WriteLine("  KEYS pattern           List keys matching pattern (* for all), e.g. KEYS *");
            Console.WriteLine("  PING                   Test server connectivity");
            Console.WriteLine("  FLUSHDB                Clear all keys from database");
            Console.WriteLine();
            Console.WriteLine("Control Commands:");
            Console.WriteLine("  ?                      Show this command menu");
            Console.WriteLine("  stackexchange | se     Run StackExchange.Redis connection sequence");
            Console.WriteLine("  quit | exit            Disconnect and exit");
            Console.WriteLine();
            Console.WriteLine("=============================");
            Console.WriteLine();
        }

        private static async Task ProcessCommandAsync(RedisClient client, string input, CancellationToken cancellationToken = default)
        {
            var parts = ParseCommand(input);
            if (parts.Length == 0) return;

            var command = parts[0].ToUpperInvariant();

            switch (command)
            {
                case "SET":
                    if (parts.Length >= 3)
                    {
                        var key = parts[1];
                        var value = string.Join(" ", parts[2..]);
                        // Remove quotes if present
                        if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length > 1)
                        {
                            value = value[1..^1];
                        }
                        await client.SetAsync(key, value, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        Console.WriteLine("Usage: SET key value");
                        Console.WriteLine("Example: SET mykey \"hello world\"");
                    }
                    break;

                case "GET":
                    if (parts.Length >= 2)
                    {
                        var key = parts[1];
                        await client.GetAsync(key, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        Console.WriteLine("Usage: GET key");
                        Console.WriteLine("Example: GET mykey");
                    }
                    break;

                case "DEL":
                    if (parts.Length >= 2)
                    {
                        var key = parts[1];
                        await client.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        Console.WriteLine("Usage: DEL key");
                        Console.WriteLine("Example: DEL mykey");
                    }
                    break;

                case "EXISTS":
                    if (parts.Length >= 2)
                    {
                        var key = parts[1];
                        await client.ExistsAsync(key, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        Console.WriteLine("Usage: EXISTS key");
                        Console.WriteLine("Example: EXISTS mykey");
                    }
                    break;

                case "KEYS":
                    var pattern = parts.Length >= 2 ? parts[1] : "*";
                    await client.KeysAsync(pattern, cancellationToken).ConfigureAwait(false);
                    break;

                case "PING":
                    await client.PingAsync(cancellationToken).ConfigureAwait(false);
                    break;

                case "FLUSHDB":
                    Console.Write("Are you sure you want to delete all keys? Type 'YES' to confirm: ");
                    var confirmation = Console.ReadLine()?.Trim();
                    if (confirmation == "YES")
                    {
                        await client.FlushDatabaseAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        Console.WriteLine("FLUSHDB cancelled");
                    }
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Type 'help' or 'menu' to see available commands");
                    break;
            }
        }

        private static string[] ParseCommand(string input)
        {
            var parts = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            bool escapeNext = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (escapeNext)
                {
                    current.Append(c);
                    escapeNext = false;
                    continue;
                }

                if (c == '\\')
                {
                    escapeNext = true;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                parts.Add(current.ToString());
            }

            return parts.ToArray();
        }
    }
}
