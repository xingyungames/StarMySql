using System.Collections.Generic;
using System.Threading.Tasks;
using Star.SqlData.Core;
using Star.SqlData.Models;
using Star.SqlData.Repositories;

namespace Star.SqlData.Sample
{
    /// <summary>
    /// Minimal repository for the demo table. The base class enforces table/column whitelists,
    /// parameterized SQL, secret masking and the unified <see cref="ApiResponse"/> envelope;
    /// this class only publishes the operations the demo needs.
    /// </summary>
    internal sealed class DemoRepository : RepositoryBase
    {
        protected override string TableName => DatabaseConstants.TableA;

        protected override HashSet<string> AllowedColumns => DatabaseConstants.TableAAllowedColumns;

        public Task<ApiResponse> QueryAsync(string column, object value, List<string>? fields = null)
            => GetDataByColumnAsync(column, value, fields);

        public Task<ApiResponse> InsertAsync(Dictionary<string, object> data)
            => CreateDataAsync(data);

        public Task<ApiResponse> UpdateAsync(string whereColumn, object whereValue, Dictionary<string, object> data)
            => UpdateDataAsync(whereColumn, whereValue, data);

        public Task<ApiResponse> DeleteAsync(string whereColumn, object whereValue)
            => DeleteDataAsync(whereColumn, whereValue);

        public Task<int> CountRowsAsync(string? whereColumn = null, object? whereValue = null)
            => CountAsync(whereColumn, whereValue);
    }
}
