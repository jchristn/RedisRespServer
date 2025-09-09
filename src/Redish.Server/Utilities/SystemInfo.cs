namespace Redish.Server.Utilities
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides system information and detection utilities for runtime-determined values.
    /// </summary>
    /// <remarks>
    /// This class contains methods to detect system characteristics such as architecture,
    /// build metadata, and other runtime-determined properties needed for Redis INFO commands.
    /// </remarks>
    public static class SystemInfo
    {
        /// <summary>
        /// Gets the architecture bit count (32 or 64) of the current process.
        /// </summary>
        /// <returns>The architecture bit count as an integer (32 or 64).</returns>
        public static int GetArchitectureBits()
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X86 => 32,
                Architecture.Arm => 32,
                Architecture.X64 => 64,
                Architecture.Arm64 => 64,
                _ => IntPtr.Size * 8
            };
        }

        /// <summary>
        /// Gets the build SHA hash from the assembly metadata.
        /// </summary>
        /// <returns>The build SHA hash as a string, or "unknown" if not available.</returns>
        public static string GetBuildSha()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                AssemblyInformationalVersionAttribute? versionAttribute = 
                    assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                
                if (versionAttribute?.InformationalVersion != null)
                {
                    string version = versionAttribute.InformationalVersion;
                    // Look for SHA in version string (format: "1.0.0+sha.abcd1234")
                    int shaIndex = version.IndexOf("+sha.", StringComparison.OrdinalIgnoreCase);
                    if (shaIndex >= 0)
                    {
                        return version.Substring(shaIndex + 5);
                    }
                }

                // Try to get from other version attributes
                string? fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
                if (!string.IsNullOrEmpty(fileVersion) && fileVersion.Contains("+"))
                {
                    string[] parts = fileVersion.Split('+');
                    if (parts.Length > 1) return parts[1];
                }

                return "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>
        /// Gets the build date from the assembly metadata.
        /// </summary>
        /// <returns>The build date as a string in Unix timestamp format.</returns>
        public static string GetBuildDate()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                
                // Try to get build date from assembly metadata
                AssemblyMetadataAttribute? buildDateAttribute = null;
                foreach (AssemblyMetadataAttribute attr in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                {
                    if (attr.Key.Equals("BuildDate", StringComparison.OrdinalIgnoreCase))
                    {
                        buildDateAttribute = attr;
                        break;
                    }
                }

                if (buildDateAttribute?.Value != null && 
                    DateTime.TryParse(buildDateAttribute.Value, out DateTime buildDate))
                {
                    return ((DateTimeOffset)buildDate).ToUnixTimeSeconds().ToString();
                }

                // Fallback: use assembly creation time
                DateTime creationTime = System.IO.File.GetCreationTimeUtc(assembly.Location);
                return ((DateTimeOffset)creationTime).ToUnixTimeSeconds().ToString();
            }
            catch
            {
                // Fallback: use current time
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            }
        }

        /// <summary>
        /// Gets the networking API being used by the current platform.
        /// </summary>
        /// <returns>The networking API name as a string.</returns>
        public static string GetNetworkingApi()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "winsock";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "epoll";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "kqueue";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                return "kqueue";
            }
            else
            {
                return "select";
            }
        }

        /// <summary>
        /// Gets the operating system name for Redis compatibility.
        /// </summary>
        /// <returns>The operating system name as a string.</returns>
        public static string GetOperatingSystemName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "Darwin";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                return "FreeBSD";
            else
                return Environment.OSVersion.Platform.ToString();
        }

        /// <summary>
        /// Gets the processor architecture as a string.
        /// </summary>
        /// <returns>The processor architecture name.</returns>
        public static string GetProcessorArchitecture()
        {
            return RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Gets the runtime framework description.
        /// </summary>
        /// <returns>The .NET runtime framework description.</returns>
        public static string GetRuntimeFramework()
        {
            return RuntimeInformation.FrameworkDescription;
        }
    }
}