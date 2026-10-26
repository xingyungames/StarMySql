using System;
using System.Threading.Tasks;
using Star.SqlData;
using Xunit;

namespace Star.SqlData.Tests
{
    /// <summary>
    /// Optional integration tests. They run only when <c>STAR_DB_CONNECTION</c> points at a
    /// reachable MySQL server; otherwise they return without failing, so the unit-test suite
    /// stays green on machines without a database.
    /// </summary>
    [Trait("Category", "Integration")]
    public class MySqlIntegrationTests
    {
        private const string ConnectionStringVariable = "STAR_DB_CONNECTION";

        private static string? ConnectionString
            => Environment.GetEnvironmentVariable(ConnectionStringVariable);

        [Fact]
        public async Task OpenAsync_ConnectsAndExecutesAScalarQuery()
        {
            string? connectionString = ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                return; // No database configured: nothing to verify.

            using var connection = await StarDataConnection.OpenAsync(connectionString);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";

            object? result = await command.ExecuteScalarAsync();

            Assert.Equal(1, Convert.ToInt32(result));
        }

        [Fact]
        public async Task OpenAsync_SurfacesConnectionFailureForBadCredentials()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                return;

            // A syntactically valid but unusable server must surface an error rather than hang.
            const string unreachable = "server=127.0.0.1;port=1;database=none;user=none;password=none;connection timeout=2";

            var ex = await Assert.ThrowsAnyAsync<Exception>(
                () => StarDataConnection.OpenAsync(unreachable));

            Assert.NotNull(ex);
        }
    }
}
