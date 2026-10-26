using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Star.SqlData.Core;
using Star.SqlData.Models;

namespace Star.SqlData.Repositories
{
    /// <summary>
    /// Repository base class providing generic CRUD operations.
    /// </summary>
    public abstract class RepositoryBase
    {
        private static readonly char[] FieldSeparators = { ',', ';' };

        protected abstract string TableName { get; }
        protected abstract HashSet<string> AllowedColumns { get; }

        /// <summary>
        /// Privileged columns: only dedicated methods (registration, password change,
        /// token update, admin operations) are allowed to write these.
        /// </summary>
        /// <remarks>Backward-compatible alias of <see cref="DatabaseConstants.PrivilegedColumns"/>.</remarks>
        public static readonly HashSet<string> PrivilegedColumns = DatabaseConstants.PrivilegedColumns;

        /// <summary>
        /// Columns that must be masked in query results (never return secrets to upper layers or clients).
        /// </summary>
        /// <remarks>Backward-compatible alias of <see cref="DatabaseConstants.MaskedColumns"/>.</remarks>
        public static readonly HashSet<string> MaskedColumns = DatabaseConstants.MaskedColumns;

        /// <summary>
        /// Validates that the table name is in the whitelist.
        /// </summary>
        protected virtual void ValidateTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name cannot be empty", nameof(tableName));
            if (!DatabaseConstants.AllowedTables.Contains(tableName))
                throw new ArgumentException($"Illegal table name: {tableName}", nameof(tableName));
        }

        /// <summary>
        /// Validates that the column name is in the whitelist.
        /// </summary>
        protected virtual void ValidateColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentException("Column name cannot be empty", nameof(columnName));
            if (!AllowedColumns.Contains(columnName) && !DatabaseConstants.GlobalAllowedColumns.Contains(columnName))
                throw new ArgumentException($"Illegal column name: {columnName}", nameof(columnName));
        }

        /// <summary>
        /// Parses a field string into a list.
        /// </summary>
        protected static List<string> ParseFieldList(string fieldsString)
        {
            if (string.IsNullOrWhiteSpace(fieldsString))
                return new List<string>();
            return fieldsString.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries)
                               .Select(f => f.Trim())
                               .Where(f => !string.IsNullOrEmpty(f))
                               .ToList();
        }

        /// <summary>
        /// Filters valid fields: trims each entry, drops anything outside the whitelists and
        /// removes duplicates (case-insensitive, first occurrence wins).
        /// </summary>
        protected List<string> FilterValidFields(List<string>? requestedFields)
        {
            if (requestedFields == null || !requestedFields.Any())
                return new List<string>();

            return requestedFields
                .Select(f => f?.Trim())
                .Where(f => !string.IsNullOrWhiteSpace(f) &&
                           (AllowedColumns.Contains(f!) || DatabaseConstants.GlobalAllowedColumns.Contains(f!)))
                .Select(f => f!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        #region Generic query methods

        /// <summary>
        /// Queries data by condition (auto-managed connection).
        /// </summary>
        protected async Task<ApiResponse> GetDataByColumnAsync(
            string column,
            object value,
            List<string>? requestedFields = null)
        {
            return await GetDataByColumnAsync(column, value, requestedFields, null, null);
        }

        /// <summary>
        /// Queries data by condition (reuses connection).
        /// </summary>
        protected async Task<ApiResponse> GetDataByColumnAsync(
            string column,
            object value,
            List<string>? requestedFields,
            MySqlConnection? connection,
            MySqlTransaction? transaction = null,
            bool maskSecrets = true,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateTable(TableName);
                ValidateColumn(column);

                var effectiveFields = FilterValidFields(requestedFields ?? new List<string>());
                if (!effectiveFields.Any())
                    return ApiResponse.BadRequest("No valid fields: none of the requested fields are in the whitelist");

                string selectFields = string.Join(", ", effectiveFields.Select(f => $"`{f}`"));
                string sql = $"SELECT {selectFields} FROM `{TableName}` WHERE `{column}` = @QueryValue LIMIT 1";

                var row = await DatabaseExecutor.ExecuteSingleRowAsync<Dictionary<string, object>>(
                    sql,
                    p => p.AddWithValue("@QueryValue", value ?? DBNull.Value),
                    reader =>
                    {
                        var result = new Dictionary<string, object>();
                        foreach (var field in effectiveFields)
                        {
                            object? cell = reader[field] == DBNull.Value ? null : reader[field];
                            if (maskSecrets && cell != null && MaskedColumns.Contains(field))
                                cell = "***";
                            // Nulls are intentional: they serialize as JSON null for the caller.
                            result[field] = cell!;
                        }
                        return result;
                    },
                    connection, transaction, cancellationToken);

                if (row == null)
                    return ApiResponse.NotFound($"{column}={value} no matching data");

                return ApiResponse.Success(row);
            }
            catch (ArgumentException ex)
            {
                // Illegal table/column name: a caller error, not a server failure.
                return ApiResponse.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return ApiResponse.ServerError($"Query failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Queries all matching data.
        /// </summary>
        protected async Task<ApiResponse> GetDataListByColumnAsync(
            string column,
            object value,
            List<string>? requestedFields = null,
            MySqlConnection? connection = null,
            MySqlTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateTable(TableName);
                ValidateColumn(column);

                var effectiveFields = FilterValidFields(requestedFields ?? new List<string>());
                if (!effectiveFields.Any())
                    return ApiResponse.BadRequest("No valid fields: none of the requested fields are in the whitelist");

                string selectFields = string.Join(", ", effectiveFields.Select(f => $"`{f}`"));

                string sql = $"SELECT {selectFields} FROM `{TableName}` WHERE `{column}` = @QueryValue";

                var rows = await DatabaseExecutor.ExecuteReaderAsync(
                    sql,
                    p => p.AddWithValue("@QueryValue", value ?? DBNull.Value),
                    connection, transaction, cancellationToken);

                if (rows.Count == 0)
                    return ApiResponse.NotFound($"No matching data: {column}={value}");

                return ApiResponse.Success(rows);
            }
            catch (ArgumentException ex)
            {
                // Illegal table/column name: a caller error, not a server failure.
                return ApiResponse.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return ApiResponse.ServerError($"Query failed: {ex.Message}");
            }
        }

        #endregion

        #region Generic insert methods

        /// <summary>
        /// Creates data (auto-managed connection).
        /// </summary>
        protected async Task<ApiResponse> CreateDataAsync(Dictionary<string, object> data)
        {
            return await CreateDataAsync(data, null, null);
        }

        /// <summary>
        /// Creates data (reuses connection).
        /// </summary>
        protected async Task<ApiResponse> CreateDataAsync(
            Dictionary<string, object> data,
            MySqlConnection? connection,
            MySqlTransaction? transaction = null,
            bool allowPrivilegedColumns = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (data == null || !data.Any())
                    return ApiResponse.BadRequest("Insert data cannot be empty");

                ValidateTable(TableName);

                var effectiveData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in data)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
                    string fieldName = kvp.Key.Trim();
                    if (!AllowedColumns.Contains(fieldName) && !DatabaseConstants.GlobalAllowedColumns.Contains(fieldName)) continue;
                    if (!allowPrivilegedColumns && PrivilegedColumns.Contains(fieldName)) continue;

                    effectiveData[fieldName] = kvp.Value ?? DBNull.Value;
                }

                if (!effectiveData.Any())
                    return ApiResponse.BadRequest("No valid fields: none of the passed fields are in the whitelist");

                string fields = string.Join(", ", effectiveData.Keys.Select(f => $"`{f}`"));
                string paramNames = string.Join(", ", effectiveData.Keys.Select(f => $"@{f}"));
                string sql = $"INSERT INTO `{TableName}` ({fields}) VALUES ({paramNames})";

                int affected = await DatabaseExecutor.ExecuteNonQueryAsync(
                    sql,
                    p =>
                    {
                        foreach (var kvp in effectiveData)
                            p.AddWithValue($"@{kvp.Key}", kvp.Value);
                    },
                    connection, transaction, cancellationToken);

                if (affected > 0)
                    return ApiResponse.Success(new { AffectedRows = affected }, "Data created successfully");

                return ApiResponse.ServerError("Data creation failed: no rows inserted");
            }
            catch (ArgumentException ex)
            {
                // Illegal table/column name: a caller error, not a server failure.
                return ApiResponse.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return ApiResponse.ServerError($"Data creation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates data and returns the auto-increment ID.
        /// </summary>
        protected async Task<int> CreateDataAndGetIdAsync(
            Dictionary<string, object> data,
            MySqlConnection? connection = null,
            MySqlTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            var response = await CreateDataAsync(data, connection, transaction, cancellationToken: cancellationToken);
            if (response.Code != "200")
                return 0;

            var idResult = await DatabaseExecutor.ExecuteScalarAsync(
                "SELECT LAST_INSERT_ID()",
                null, connection, transaction, cancellationToken);

            return idResult != null ? Convert.ToInt32(idResult, CultureInfo.InvariantCulture) : 0;
        }

        #endregion

        #region Generic update methods

        /// <summary>
        /// Updates data (auto-managed connection).
        /// </summary>
        protected async Task<ApiResponse> UpdateDataAsync(
            string whereColumn,
            object whereValue,
            Dictionary<string, object> updateData)
        {
            return await UpdateDataAsync(whereColumn, whereValue, updateData, null, null);
        }

        /// <summary>
        /// Updates data (reuses connection).
        /// </summary>
        protected async Task<ApiResponse> UpdateDataAsync(
            string whereColumn,
            object whereValue,
            Dictionary<string, object> updateData,
            MySqlConnection? connection,
            MySqlTransaction? transaction = null,
            bool allowPrivilegedColumns = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (whereValue == null || string.IsNullOrWhiteSpace(whereValue.ToString()))
                    return ApiResponse.BadRequest("Update condition value cannot be empty");
                if (updateData == null || !updateData.Any())
                    return ApiResponse.BadRequest("Update data cannot be empty");

                ValidateTable(TableName);
                ValidateColumn(whereColumn);

                var effectiveData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in updateData)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
                    string fieldName = kvp.Key.Trim();
                    if (string.IsNullOrWhiteSpace(fieldName)) continue;
                    if (!AllowedColumns.Contains(fieldName) && !DatabaseConstants.GlobalAllowedColumns.Contains(fieldName)) continue;
                    // Do not allow the WHERE column to be overwritten (primary key / ownership).
                    if (fieldName.Equals(whereColumn, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!allowPrivilegedColumns && PrivilegedColumns.Contains(fieldName)) continue;

                    effectiveData[fieldName] = kvp.Value ?? DBNull.Value;
                }

                if (!effectiveData.Any())
                    return ApiResponse.BadRequest("No valid update fields");

                string setClause = string.Join(", ", effectiveData.Keys.Select(f => $"`{f}` = @Update_{f}"));
                string sql = $"UPDATE `{TableName}` SET {setClause} WHERE `{whereColumn}` = @Where_Value LIMIT 1";

                int affected = await DatabaseExecutor.ExecuteNonQueryAsync(
                    sql,
                    p =>
                    {
                        foreach (var kvp in effectiveData)
                            p.AddWithValue($"@Update_{kvp.Key}", kvp.Value);
                        p.AddWithValue("@Where_Value", whereValue ?? DBNull.Value);
                    },
                    connection, transaction, cancellationToken);

                if (affected > 0)
                    return ApiResponse.Success(new { AffectedRows = affected }, "Data updated successfully");

                return ApiResponse.NotFound($"No record found matching {whereColumn}={whereValue}");
            }
            catch (ArgumentException ex)
            {
                // Illegal table/column name: a caller error, not a server failure.
                return ApiResponse.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return ApiResponse.ServerError($"Data update failed: {ex.Message}");
            }
        }

        #endregion

        #region Generic delete methods

        /// <summary>
        /// Deletes data.
        /// </summary>
        protected async Task<ApiResponse> DeleteDataAsync(
            string whereColumn,
            object whereValue,
            MySqlConnection? connection = null,
            MySqlTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateTable(TableName);
                ValidateColumn(whereColumn);

                string sql = $"DELETE FROM `{TableName}` WHERE `{whereColumn}` = @Where_Value LIMIT 1";

                int affected = await DatabaseExecutor.ExecuteNonQueryAsync(
                    sql,
                    p => p.AddWithValue("@Where_Value", whereValue ?? DBNull.Value),
                    connection, transaction, cancellationToken);

                if (affected > 0)
                    return ApiResponse.Success(new { AffectedRows = affected }, "Data deleted successfully");

                return ApiResponse.NotFound($"No record found matching {whereColumn}={whereValue}");
            }
            catch (ArgumentException ex)
            {
                // Illegal table/column name: a caller error, not a server failure.
                return ApiResponse.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return ApiResponse.ServerError($"Data deletion failed: {ex.Message}");
            }
        }

        #endregion

        #region Count methods

        /// <summary>
        /// Gets the record count.
        /// </summary>
        protected async Task<int> CountAsync(
            string? whereColumn = null,
            object? whereValue = null,
            MySqlConnection? connection = null,
            MySqlTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateTable(TableName);

                string sql;
                if (!string.IsNullOrWhiteSpace(whereColumn))
                {
                    ValidateColumn(whereColumn);
                    sql = $"SELECT COUNT(1) FROM `{TableName}` WHERE `{whereColumn}` = @Where_Value";
                    var result = await DatabaseExecutor.ExecuteScalarAsync(
                        sql,
                        p => p.AddWithValue("@Where_Value", whereValue ?? DBNull.Value),
                        connection, transaction, cancellationToken);
                    return result != null ? Convert.ToInt32(result, CultureInfo.InvariantCulture) : 0;
                }
                else
                {
                    sql = $"SELECT COUNT(1) FROM `{TableName}`";
                    var result = await DatabaseExecutor.ExecuteScalarAsync(
                        sql, null, connection, transaction, cancellationToken);
                    return result != null ? Convert.ToInt32(result, CultureInfo.InvariantCulture) : 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }
}