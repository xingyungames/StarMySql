using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace Star.SqlData.Core
{
    /// <summary>
    /// Pure database executor. Contains no business semantics.
    /// </summary>
    /// <remarks>
    /// The connection string is resolved lazily on first use, so loading this assembly never throws
    /// when no configuration is present.
    /// </remarks>
    internal static class DatabaseExecutor
    {
        /// <summary>
        /// Gets an already-open database connection.
        /// </summary>
        public static Task<MySqlConnection> GetOpenConnectionAsync()
            => StarDataConnection.OpenAsync();

        /// <summary>
        /// Executes a database command.
        /// </summary>
        internal static async Task<T> ExecuteCommandAsync<T>(
            Func<MySqlCommand, Task<T>> action,
            string sql,
            Action<MySqlParameterCollection>? addParameters = null,
            MySqlConnection? existingConnection = null,
            MySqlTransaction? existingTransaction = null,
            CancellationToken cancellationToken = default)
        {
            bool ownsConnection = existingConnection == null;
            var connection = existingConnection ?? await GetOpenConnectionAsync();
            try
            {
                using var command = new MySqlCommand(sql, connection, existingTransaction);
                command.CommandTimeout = 30;
                addParameters?.Invoke(command.Parameters);
                return await action(command);
            }
            finally
            {
                if (ownsConnection && connection != null)
                    await connection.DisposeAsync();
            }
        }

        /// <summary>
        /// Executes a non-query command.
        /// </summary>
        internal static async Task<int> ExecuteNonQueryAsync(
            string sql,
            Action<MySqlParameterCollection>? addParameters = null,
            MySqlConnection? connection = null,
            MySqlTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteCommandAsync(
                async cmd => await cmd.ExecuteNonQueryAsync(cancellationToken),
                sql, addParameters, connection, transaction, cancellationToken);
        }

        /// <summary>
        /// Executes a scalar query.
        /// </summary>
        internal static async Task<object?> ExecuteScalarAsync(
            string sql,
            Action<MySqlParameterCollection>? addParameters = null,
            MySqlConnection? connection = null,
            MySqlTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteCommandAsync(
                async cmd => await cmd.ExecuteScalarAsync(cancellationToken),
                sql, addParameters, connection, transaction, cancellationToken);
        }

        /// <summary>
        /// Executes a query and returns a list of dictionaries.
        /// </summary>
        internal static async Task<List<Dictionary<string, object>>> ExecuteReaderAsync(
            string sql,
            Action<MySqlParameterCollection>? addParameters = null,
            MySqlConnection? connection = null,
            MySqlTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteCommandAsync(
                async cmd =>
                {
                    var results = new List<Dictionary<string, object>>();
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string fieldName = reader.GetName(i);
                            object value = reader.GetValue(i);
                            // Mask columns listed in the masked-column set before leaving the executor.
                            if (DatabaseConstants.IsMaskedColumn(fieldName))
                                value = "***";
                            row[fieldName] = value;
                        }
                        results.Add(row);
                    }
                    return results;
                },
                sql, addParameters, connection, transaction, cancellationToken);
        }

        /// <summary>
        /// Executes a query and returns a single mapped row (using DbDataReader).
        /// </summary>
        internal static async Task<T?> ExecuteSingleRowAsync<T>(
            string sql,
            Action<MySqlParameterCollection>? addParameters = null,
            Func<DbDataReader, T>? mapRow = null,
            MySqlConnection? connection = null,
            MySqlTransaction? transaction = null,
            CancellationToken cancellationToken = default) where T : class
        {
            return await ExecuteCommandAsync(
                async cmd =>
                {
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    if (await reader.ReadAsync(cancellationToken))
                        return mapRow?.Invoke(reader);
                    return null;
                },
                sql, addParameters, connection, transaction, cancellationToken);
        }

        /// <summary>
        /// Executes a query and returns a list of mapped rows (using DbDataReader).
        /// </summary>
        internal static async Task<List<T>> ExecuteReaderAsync<T>(
            string sql,
            Action<MySqlParameterCollection>? addParameters = null,
            Func<DbDataReader, T>? mapRow = null,
            MySqlConnection? connection = null,
            MySqlTransaction? transaction = null,
            CancellationToken cancellationToken = default) where T : class
        {
            return await ExecuteCommandAsync(
                async cmd =>
                {
                    var results = new List<T>();
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        if (mapRow != null)
                            results.Add(mapRow(reader));
                    }
                    return results;
                },
                sql, addParameters, connection, transaction, cancellationToken);
        }
    }
}