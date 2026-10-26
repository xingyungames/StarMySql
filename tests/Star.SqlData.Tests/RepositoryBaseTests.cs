using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Star.SqlData.Core;
using Star.SqlData.Models;
using Star.SqlData.Repositories;
using Xunit;

namespace Star.SqlData.Tests
{
    /// <summary>
    /// Unit tests for the whitelist validation layer of <see cref="RepositoryBase"/>.
    /// Every assertion here is database-free: invalid input must be rejected before any
    /// connection is opened, so these tests pass on a machine without MySQL.
    /// </summary>
    public class RepositoryBaseTests
    {
        [Theory]
        [InlineData(DatabaseConstants.TableA)]
        [InlineData(DatabaseConstants.TableB)]
        [InlineData(DatabaseConstants.TableC)]
        public async Task Insert_WithWhitelistedData_PassesValidation(string tableName)
        {
            var repository = new TestRepository(tableName);

            // Either the connection fails (no database) or the insert succeeds (live database),
            // but the request must never be rejected by the whitelist.
            var response = await repository.InsertAsync(new Dictionary<string, object> { ["col_a"] = "value" });

            Assert.NotEqual("400", response.Code);
        }

        [Fact]
        public async Task Insert_WithIllegalTable_ReturnsBadRequest()
        {
            var repository = new TestRepository("table_not_whitelisted");

            var response = await repository.InsertAsync(new Dictionary<string, object> { ["col_a"] = "value" });

            Assert.Equal("400", response.Code);
            Assert.Contains("Illegal table name", response.Message);
        }

        [Fact]
        public async Task Query_WithIllegalColumn_ReturnsBadRequest()
        {
            var repository = new TestRepository();

            var response = await repository.QueryAsync("col_x; DROP TABLE table_a", "value",
                new List<string> { "col_a" });

            Assert.Equal("400", response.Code);
            Assert.Contains("Illegal column name", response.Message);
        }

        [Fact]
        public async Task Query_WithEmptyFieldList_ReturnsBadRequest()
        {
            var repository = new TestRepository();

            var response = await repository.QueryAsync("col_a", "value", new List<string>());

            Assert.Equal("400", response.Code);
            Assert.Contains("No valid fields", response.Message);
        }

        [Fact]
        public void Query_KeepsOnlyWhitelistedFields()
        {
            var repository = new TestRepository();

            var filtered = repository.FilterFields(new List<string>
            {
                "col_a", "col_b", "not_a_column", "col_a", "  col_c  ", "",
            });

            Assert.Equal(new[] { "col_a", "col_b", "col_c" }, filtered);
        }

        [Fact]
        public void ParseFieldList_SplitsOnCommasAndSemicolons()
        {
            Assert.Equal(new[] { "col_a", "col_b", "col_c" },
                TestRepository.ParseFields("col_a, col_b;col_c"));
            Assert.Empty(TestRepository.ParseFields("   "));
            Assert.Empty(TestRepository.ParseFields(""));
        }

        [Fact]
        public async Task Insert_WithPrivilegedColumnOnly_ReturnsBadRequest()
        {
            var repository = new TestRepository();

            var response = await repository.InsertAsync(new Dictionary<string, object>
            {
                [DatabaseConstants.PrivilegedColumns.Min()!] = "secret",
            });

            Assert.Equal("400", response.Code);
            Assert.Contains("No valid fields", response.Message);
        }

        [Fact]
        public async Task Insert_WithEmptyData_ReturnsBadRequest()
        {
            var repository = new TestRepository();

            var response = await repository.InsertAsync(new Dictionary<string, object>());

            Assert.Equal("400", response.Code);
            Assert.Contains("cannot be empty", response.Message);
        }

        [Fact]
        public async Task Update_WithEmptyWhereValue_ReturnsBadRequest()
        {
            var repository = new TestRepository();

            var response = await repository.UpdateAsync("col_a", "  ",
                new Dictionary<string, object> { ["col_b"] = "value" });

            Assert.Equal("400", response.Code);
        }

        [Fact]
        public async Task Update_WithEmptyData_ReturnsBadRequest()
        {
            var repository = new TestRepository();

            var response = await repository.UpdateAsync("col_a", "id-1",
                new Dictionary<string, object>());

            Assert.Equal("400", response.Code);
        }

        [Fact]
        public async Task Update_IgnoresWhereColumnAndPrivilegedColumnsInPayload()
        {
            var repository = new TestRepository();

            // Only ignored fields are passed: the method must report that nothing is updatable.
            var response = await repository.UpdateAsync("col_a", "id-1", new Dictionary<string, object>
            {
                ["col_a"] = "hijack",
                [DatabaseConstants.PrivilegedColumns.Min()!] = "secret",
            });

            Assert.Equal("400", response.Code);
            Assert.Contains("No valid update fields", response.Message);
        }

        [Fact]
        public async Task Delete_WithIllegalColumn_ReturnsBadRequest()
        {
            var repository = new TestRepository();

            var response = await repository.DeleteAsync("col_zz", "value");

            Assert.Equal("400", response.Code);
            Assert.Contains("Illegal column name", response.Message);
        }

        [Fact]
        public async Task Count_WithoutDatabase_ReturnsZero()
        {
            var repository = new TestRepository();

            Assert.Equal(0, await repository.CountRowsAsync());
        }

        /// <summary>
        /// Minimal repository used to expose the protected members under test.
        /// </summary>
        private sealed class TestRepository : RepositoryBase
        {
            private readonly string _tableName;

            public TestRepository(string tableName = DatabaseConstants.TableA) => _tableName = tableName;

            protected override string TableName => _tableName;

            protected override HashSet<string> AllowedColumns => DatabaseConstants.TableAAllowedColumns;

            public List<string> FilterFields(List<string>? fields) => FilterValidFields(fields);

            public static List<string> ParseFields(string fields) => ParseFieldList(fields);

            public Task<ApiResponse> QueryAsync(string column, object value, List<string>? fields)
                => GetDataByColumnAsync(column, value, fields);

            public Task<ApiResponse> InsertAsync(Dictionary<string, object> data)
                => CreateDataAsync(data);

            public Task<ApiResponse> UpdateAsync(string column, object value, Dictionary<string, object> data)
                => UpdateDataAsync(column, value, data);

            public Task<ApiResponse> DeleteAsync(string column, object value)
                => DeleteDataAsync(column, value);

            public Task<int> CountRowsAsync(string? column = null, object? value = null)
                => CountAsync(column, value);
        }
    }
}
