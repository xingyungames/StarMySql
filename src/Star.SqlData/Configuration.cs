using System;
using System.Configuration;

// Application configuration entry point (minimal set: database connection only).
namespace Star.App.Configuration
{
    /// <summary>
    /// Reads the MySQL connection configuration used by <c>Star.SqlData</c>.
    /// </summary>
    public static class Configuration
    {
        /// <summary>
        /// Server configuration.
        /// </summary>
        public static class ServerConfiguration
        {
            /// <summary>
            /// Builds the MySQL connection string.
            /// Reads the full connection string from the STAR_DB_CONNECTION environment variable (recommended for production);
            /// otherwise assembles it field by field from appSettings (local development).
            /// </summary>
            /// <exception cref="InvalidOperationException">Thrown when a required setting is missing.</exception>
            public static string GetConnectionString()
            {
                // Prefer a full connection string from the environment to avoid storing credentials on disk.
                var full = Environment.GetEnvironmentVariable("STAR_DB_CONNECTION");
                if (!string.IsNullOrWhiteSpace(full))
                    return full;

                // Fall back to reading individual fields from appSettings.
                string serverIP = Required("DB_ServerIP");
                string port = Required("DB_Port");
                string database = Required("DB_Database");
                string user = Required("DB_User");
                string password = ConfigurationManager.AppSettings["DB_Password"] ?? string.Empty;
                string charset = Optional("DB_Charset", "utf8mb4");
                string maxPoolSize = Optional("DB_MaxPoolSize", "100");
                string minPoolSize = Optional("DB_MinPoolSize", "5");
                string connectionTimeout = Optional("DB_ConnectionTimeout", "30");

                return $"server={serverIP};" +
                       $"port={port};" +
                       $"database={database};" +
                       $"user={user};" +
                       $"password={password};" +
                       $"charset={charset};" +
                       $"pooling=true;" +
                       $"max pool size={maxPoolSize};" +
                       $"min pool size={minPoolSize};" +
                       $"connection timeout={connectionTimeout}";
            }

            /// <summary>
            /// Returns the required setting value, or throws if it is missing or empty.
            /// </summary>
            private static string Required(string key)
            {
                var v = ConfigurationManager.AppSettings[key];
                if (string.IsNullOrWhiteSpace(v))
                    throw new InvalidOperationException($"Configuration key '{key}' is missing or empty");
                return v;
            }

            /// <summary>
            /// Returns the setting value, or the default value if it is missing or empty.
            /// </summary>
            private static string Optional(string key, string defaultValue)
            {
                var v = ConfigurationManager.AppSettings[key];
                return string.IsNullOrWhiteSpace(v) ? defaultValue : v;
            }
        }
    }
}
