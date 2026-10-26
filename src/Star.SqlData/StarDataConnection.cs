using System;
using MySql.Data.MySqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Star.SqlData
{
    /// <summary>
    /// Public entry point for creating database connections.
    /// </summary>
    /// <remarks>
    /// The connection string is resolved in this order:
    /// <list type="number">
    ///   <item><description><c>STAR_DB_CONNECTION</c> environment variable (a complete connection string, recommended for production).</description></item>
    ///   <item><description><c>appSettings</c> keys (<c>DB_ServerIP</c>, <c>DB_Port</c>, ...), useful for local development. See <c>app.config</c> in the samples.</description></item>
    /// </list>
    /// </remarks>
    public static class StarDataConnection
    {
        /// <summary>
        /// Resolves the MySQL connection string from environment variables or configuration files.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a required setting is missing.</exception>
        public static string GetConnectionString()
            => Star.App.Configuration.Configuration.ServerConfiguration.GetConnectionString();

        /// <summary>
        /// Creates and opens a new MySQL connection. The caller owns the connection and must dispose it
        /// (prefer a <c>using</c> statement).
        /// </summary>
        public static async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(GetConnectionString());
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
            return connection;
        }

        /// <summary>
        /// Creates and opens a new MySQL connection using an explicit connection string,
        /// bypassing configuration files entirely (handy for tests and hosting scenarios).
        /// </summary>
        public static async Task<MySqlConnection> OpenAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

            var connection = new MySqlConnection(connectionString);
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
