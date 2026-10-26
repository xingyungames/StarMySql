using System.Collections.Generic;

namespace Star.SqlData.Core
{
    /// <summary>
    /// Database constant definitions.
    /// </summary>
    public static class DatabaseConstants
    {
        public const string TableA = "table_a";
        public const string TableB = "table_b";
        public const string TableC = "table_c";
        public const string TableD = "table_d";
        public const string TableE = "table_e";
        public const string TableF = "table_f";
        public const string TableG = "table_g";

        #region Table name constants

        public const string YourTableName = "your_table_name";

        #endregion

        #region Global column whitelist

        public static readonly HashSet<string> GlobalAllowedColumns = new HashSet<string>
        {
            "col_a", "col_b", "col_c", "col_d", "col_e",
            "col_f", "col_g", "col_h", "col_i", "col_j",
            "col_k", "col_l", "col_m", "col_n", "col_o",
            "col_p", "col_q", "col_r", "col_s", "col_t",
            "col_u", "col_v", "col_w", "col_x", "col_y",
            "col_z", "col_aa", "col_ab", "col_ac", "col_ad",
            "col_ae", "col_af", "col_ag", "col_ah",
            "col_ai", "col_aj", "col_ak", "col_al",
            "col_am", "col_an", "col_ao", "col_ap", "col_aq", "col_ar"
        };

        #endregion

        #region Per-table allowed columns

        public static readonly HashSet<string> TableAAllowedColumns = new HashSet<string>
        {
            "col_a", "col_b", "col_c", "col_d", "col_e",
            "col_f", "col_g", "col_h", "col_i", "col_j",
            "col_k", "col_l", "col_m", "col_n", "col_o",
            "col_p", "col_q"
        };

        public static readonly HashSet<string> TableBAllowedColumns = new HashSet<string>
        {
            "col_r", "col_s", "col_t", "col_u", "col_v", "col_w"
        };

        public static readonly HashSet<string> TableCAllowedColumns = new HashSet<string>
        {
            "col_r", "col_x"
        };

        public static readonly HashSet<string> TableDAllowedColumns = new HashSet<string>
        {
            "col_y", "col_z", "col_aa", "col_c", "col_ab", "col_ac"
        };

        #endregion

        #region Table whitelist

        public static readonly HashSet<string> AllowedTables = new HashSet<string>
        {
            TableA,
            TableB,
            TableD,
            TableE,
            TableC,
            TableF,
            TableG
        };

        #endregion

        #region Privileged and masked columns

        /// <summary>
        /// Privileged columns: only dedicated methods (registration, password change, token update,
        /// administrative operations) may write them. Comparisons are case-insensitive.
        /// </summary>
        public static readonly HashSet<string> PrivilegedColumns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "privileged_col_1", "privileged_col_2", "privileged_col_3", "privileged_col_4",
            "privileged_col_5", "privileged_col_6", "privileged_col_7", "privileged_col_8",
        };

        /// <summary>
        /// Columns masked in every query result (secrets must never reach upper layers or clients).
        /// Comparisons are case-insensitive.
        /// </summary>
        public static readonly HashSet<string> MaskedColumns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "sensitive_col",
        };

        /// <summary>
        /// Returns <c>true</c> when the column must be masked in query results.
        /// </summary>
        public static bool IsMaskedColumn(string? columnName)
            => !string.IsNullOrWhiteSpace(columnName) && MaskedColumns.Contains(columnName);

        /// <summary>
        /// Returns <c>true</c> when the column may only be written by dedicated methods.
        /// </summary>
        public static bool IsPrivilegedColumn(string? columnName)
            => !string.IsNullOrWhiteSpace(columnName) && PrivilegedColumns.Contains(columnName);

        #endregion
    }
}