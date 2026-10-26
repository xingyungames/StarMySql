using System;
using System.Threading.Tasks;
using Star.SqlData;
using Xunit;

namespace Star.SqlData.Tests
{
    /// <summary>
    /// Collection that disables parallel execution: these tests mutate process-wide
    /// environment variables.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class EnvironmentVariableTestsDefinition
    {
        public const string Name = "Environment variables";
    }

    /// <summary>
    /// Connection-string resolution is the single configuration surface of the library.
    /// </summary>
    [Collection(EnvironmentVariableTestsDefinition.Name)]
    public class ConfigurationTests
    {
        private const string ConnectionStringVariable = "STAR_DB_CONNECTION";

        [Fact]
        public void GetConnectionString_ReturnsEnvironmentVariableAsIs()
        {
            const string expected = "server=db.internal;port=3307;database=star_db;user=app;password=secret";

            RunWithConnectionStringVariable(expected, () =>
            {
                Assert.Equal(expected, StarDataConnection.GetConnectionString());
            });
        }

        [Fact]
        public void GetConnectionString_IgnoresWhitespaceOnlyEnvironmentVariable()
        {
            RunWithConnectionStringVariable("   ", () =>
            {
                // Falls back to appSettings, which are absent in the test host.
                var ex = Assert.Throws<InvalidOperationException>(
                    () => StarDataConnection.GetConnectionString());

                Assert.Contains("DB_ServerIP", ex.Message, StringComparison.Ordinal);
            });
        }

        [Fact]
        public void GetConnectionString_ThrowsWhenNoConfigurationIsAvailable()
        {
            RunWithConnectionStringVariable(null, () =>
            {
                var ex = Assert.Throws<InvalidOperationException>(
                    () => StarDataConnection.GetConnectionString());

                Assert.Contains("DB_ServerIP", ex.Message, StringComparison.Ordinal);
            });
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task OpenAsync_WithEmptyConnectionString_ThrowsArgumentException(string connectionString)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => StarDataConnection.OpenAsync(connectionString));
        }

        /// <summary>
        /// Sets <c>STAR_DB_CONNECTION</c> for the duration of the action and always restores it.
        /// </summary>
        private static void RunWithConnectionStringVariable(string? value, Action action)
        {
            string? original = Environment.GetEnvironmentVariable(ConnectionStringVariable);
            try
            {
                Environment.SetEnvironmentVariable(ConnectionStringVariable, value);
                action();
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConnectionStringVariable, original);
            }
        }
    }
}
