namespace Redish.Server
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Constants.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Software version.
        /// </summary>
        public static string Version = "v0.1.0";

        /// <summary>
        /// ANSI art logo.
        /// </summary>
        public static string AnsiArt =                
            @"   ▄▀▄           " + Environment.NewLine +
            @"   ▀▄ ▀▄  ▄▀▄    " + Environment.NewLine +
            @"   ▄▄█ █▄▀ ▄▄▀   " + Environment.NewLine +
            @"  █▄▄▀█  ▄▀      " + Environment.NewLine +
            @"    ▄▄▀▀▀▀▄▄                     ___      __  " + Environment.NewLine +
            @"  ▄▀      ▄ ▀▄       _______ ___/ (_) __ / /  " + Environment.NewLine +
            @" ▄▀        █ ▀▄     / __/ -_) _  / (_-</ _ \  " + Environment.NewLine +
            @" █            █    /_/  \__/\_,_/_/___/_//_/  " + Environment.NewLine +
            @"  █          █   " + Environment.NewLine +
            @"   ▀▄▄     ▄▀    " + Environment.NewLine +
            @"      ▀▀▄▄▀      " + Environment.NewLine + Environment.NewLine;                

        /// <summary>
        /// Logo.
        /// </summary>
        public static string Logo =
            @"               ___      __   " + Environment.NewLine +
            @"   _______ ___/ (_) __ / /  " + Environment.NewLine +
            @"  / __/ -_) _  / (_-</ _ \  " + Environment.NewLine +
            @" /_/  \__/\_,_/_/___/_//_/  " + Environment.NewLine;


        /// <summary>
        /// Timestamp format.
        /// </summary>
        public static string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";

        /// <summary>
        /// Log filename.
        /// </summary>
        public static string LogFilename = "./redish.log";

        /// <summary>
        /// Log directory.
        /// </summary>
        public static string LogDirectory = "./logs/";

        /// <summary>
        /// Product name.
        /// </summary>
        public static string ProductName = "Redish Server";

        /// <summary>
        /// Settings file.
        /// </summary>
        public static string SettingsFile = "./redish.json";

        /// <summary>
        /// Copyright.
        /// </summary>
        public static string Copyright = "(c)2025 Joel Christner";

        /// <summary>
        /// Default HTML homepage.
        /// </summary>
        public static string HtmlHomepage =
            @"<html>" + Environment.NewLine +
            @"  <head>" + Environment.NewLine +
            @"    <title>Node is Operational</title>" + Environment.NewLine +
            @"  </head>" + Environment.NewLine +
            @"  <body>" + Environment.NewLine +
            @"    <div>" + Environment.NewLine +
            @"      <pre>" + Environment.NewLine + Environment.NewLine +
            Logo + Environment.NewLine +
            @"      </pre>" + Environment.NewLine +
            @"    </div>" + Environment.NewLine +
            @"    <div style='font-family: Arial, sans-serif;'>" + Environment.NewLine +
            @"      <h2>Your node is operational</h2>" + Environment.NewLine +
            @"      <p>Congratulations, your node is operational.  Please refer to the documentation for use.</p>" + Environment.NewLine +
            @"    <div>" + Environment.NewLine +
            @"  </body>" + Environment.NewLine +
            @"</html>" + Environment.NewLine;
    }
}
