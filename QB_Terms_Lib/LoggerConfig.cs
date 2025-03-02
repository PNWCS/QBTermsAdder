using Serilog;

namespace QB_Terms_Lib
{
    public static class LoggerConfig
    {
        private static bool _isInitialized = false; // Ensures logging is set up only once

        public static void ConfigureLogging()
        {
            if (_isInitialized) return; // Prevents duplicate initialization

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug() // Capture Debug, Info, and Error logs
                .WriteTo.Console() // Console will display ALL log levels
                .WriteTo.File("logs/qb_terms_lib.log",
                    rollingInterval: RollingInterval.Day,  // Create a new log file each day
                    retainedFileCountLimit: 7,             // Keep logs for 7 days
                    fileSizeLimitBytes: 5_000_000,         // Max log file size: 5MB
                    rollOnFileSizeLimit: true,             // Create a new file if it exceeds size limit
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information) // Only Info & Errors go to File
                .CreateLogger();

            _isInitialized = true; // Mark as initialized
        }
    }

    public static class AppConfig
    {
        public static string QB_APP_NAME = "TermSync";
    }
}