using System.Collections.Generic;
using System.Linq;
using Star.SqlData.Core;
using Xunit;

namespace Star.SqlData.Tests
{
    /// <summary>
    /// Guards the whitelist definitions. These are the security boundary of the library,
    /// so they are asserted explicitly instead of being trusted.
    /// </summary>
    public class DatabaseConstantsTests
    {
        public static IEnumerable<object[]> TableConstants => new[]
        {
            new object[] { DatabaseConstants.TableA },
            new object[] { DatabaseConstants.TableB },
            new object[] { DatabaseConstants.TableC },
            new object[] { DatabaseConstants.TableD },
            new object[] { DatabaseConstants.TableE },
            new object[] { DatabaseConstants.TableF },
            new object[] { DatabaseConstants.TableG },
        };

        [Theory]
        [MemberData(nameof(TableConstants))]
        public void EveryTableConstant_IsInTheTableWhitelist(string tableName)
        {
            Assert.False(string.IsNullOrWhiteSpace(tableName));
            Assert.Contains(tableName, DatabaseConstants.AllowedTables);
        }

        [Fact]
        public void TableWhitelist_ContainsNoNullOrEmptyEntries()
        {
            Assert.NotEmpty(DatabaseConstants.AllowedTables);
            Assert.All(DatabaseConstants.AllowedTables, t => Assert.False(string.IsNullOrWhiteSpace(t)));
        }

        [Fact]
        public void GlobalColumnWhitelist_IsNotEmpty()
        {
            Assert.NotEmpty(DatabaseConstants.GlobalAllowedColumns);
            Assert.All(DatabaseConstants.GlobalAllowedColumns, c => Assert.False(string.IsNullOrWhiteSpace(c)));
        }

        [Fact]
        public void PerTableColumns_AreAllCoveredByTheGlobalWhitelist()
        {
            var perTableColumns = new[]
            {
                DatabaseConstants.TableAAllowedColumns,
                DatabaseConstants.TableBAllowedColumns,
                DatabaseConstants.TableCAllowedColumns,
                DatabaseConstants.TableDAllowedColumns,
            };

            foreach (var columns in perTableColumns)
            {
                Assert.NotEmpty(columns);
                var notInGlobal = columns.Except(DatabaseConstants.GlobalAllowedColumns).ToArray();
                Assert.True(notInGlobal.Length == 0,
                    $"Columns missing from GlobalAllowedColumns: {string.Join(", ", notInGlobal)}");
            }
        }

        [Fact]
        public void MaskedColumns_AreDetectedCaseInsensitively()
        {
            Assert.All(DatabaseConstants.MaskedColumns,
                c => Assert.True(DatabaseConstants.IsMaskedColumn(c.ToUpperInvariant())));

            Assert.False(DatabaseConstants.IsMaskedColumn(null));
            Assert.False(DatabaseConstants.IsMaskedColumn("   "));
            Assert.False(DatabaseConstants.IsMaskedColumn("col_a"));
        }

        [Fact]
        public void PrivilegedColumns_AreDetectedCaseInsensitively()
        {
            Assert.All(DatabaseConstants.PrivilegedColumns,
                c => Assert.True(DatabaseConstants.IsPrivilegedColumn(c.ToUpperInvariant())));

            Assert.False(DatabaseConstants.IsPrivilegedColumn(null));
            Assert.False(DatabaseConstants.IsPrivilegedColumn("col_a"));
        }

        [Fact]
        public void RepositoryBaseAliases_ShareTheSameInstances()
        {
            Assert.Same(DatabaseConstants.PrivilegedColumns, Repositories.RepositoryBase.PrivilegedColumns);
            Assert.Same(DatabaseConstants.MaskedColumns, Repositories.RepositoryBase.MaskedColumns);
        }
    }
}
