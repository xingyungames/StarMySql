using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Star.SqlData.Core;
using Star.SqlData.Models;
using Star.SqlData.Repositories;

namespace Star.SqlData.Sample
{
    /// <summary>
    /// Console demo: creates a tiny demo table, then walks through insert / query / update / delete
    /// using a repository derived from <see cref="RepositoryBase"/>.
    /// </summary>
    /// <remarks>
    /// Run it with <c>STAR_DB_CONNECTION</c> pointing at a MySQL server, for example:
    /// <code>
    /// $env:STAR_DB_CONNECTION = "server=127.0.0.1;port=3306;database=star_db;user=star_app;password=secret"
    /// dotnet run --project samples/Star.SqlData.Sample
    /// </code>
    /// </remarks>
    internal static class Program
    {
        private const string DemoTable = DatabaseConstants.TableA;
        private const string EnvironmentVariable = "STAR_DB_CONNECTION";

        private static async Task<int> Main()
        {
            Console.WriteLine("== Star.SqlData sample ==");
            Console.WriteLine($"Demo table: {DemoTable}");
            Console.WriteLine();

            string? connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine($"{EnvironmentVariable} is not set, nothing to do.");
                Console.WriteLine("Set it to a MySQL connection string and run again, e.g.:");
                Console.WriteLine("  $env:STAR_DB_CONNECTION = \"server=127.0.0.1;port=3306;database=star_db;user=star_app;password=secret\"");
                return 0;
            }

            try
            {
                // 1) A connection opened through the public entry point (explicit connection string).
                await using MySqlConnection connection = await StarDataConnection.OpenAsync(connectionString);
                await EnsureDemoSchemaAsync(connection).ConfigureAwait(false);

                var repository = new DemoRepository();

                // 2) Insert: only columns present in the whitelist survive.
                Print(await repository.InsertAsync(new Dictionary<string, object>
                {
                    ["col_a"] = "player-1",
                    ["col_b"] = "张三",
                    ["col_c"] = 3,
                    ["not_a_column"] = "dropped silently",
                }).ConfigureAwait(false));

                // 3) Query one row and select only whitelisted fields.
                Print(await repository.QueryAsync("col_a", "player-1",
                    new List<string> { "col_a", "col_b", "col_c" }).ConfigureAwait(false));

                // 4) Update, then count the matching rows.
                Print(await repository.UpdateAsync("col_a", "player-1",
                    new Dictionary<string, object> { ["col_b"] = "李四" }).ConfigureAwait(false));
                Console.WriteLine($"Count(col_a = player-1) = {await repository.CountRowsAsync("col_a", "player-1").ConfigureAwait(false)}");

                // 5) Delete by whitelisted key column.
                Print(await repository.DeleteAsync("col_a", "player-1").ConfigureAwait(false));

                Console.WriteLine();
                Console.WriteLine("Sample finished successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Sample failed: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Creates the demo table when it is missing. The library never issues DDL,
        /// so the sample owns this setup step.
        /// </summary>
        private static async Task EnsureDemoSchemaAsync(MySqlConnection connection)
        {
            const string ddl = @"
CREATE TABLE IF NOT EXISTS `table_a` (
    `col_a` VARCHAR(64) NOT NULL PRIMARY KEY,
    `col_b` VARCHAR(255) NULL,
    `col_c` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            await using var command = new MySqlCommand(ddl, connection);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static void Print(ApiResponse response)
        {
            Console.WriteLine($"-> {response.Code} {response.Message}");
            Console.WriteLine(response.ToJson());
            Console.WriteLine();
        }
    }
}
