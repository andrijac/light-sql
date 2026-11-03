using System.Data;

namespace LightSQL.Models
{
    /// <summary>
    /// The outcome of executing a SQL batch: any returned result sets, the
    /// informational/error messages, how many rows were affected, and timing.
    /// </summary>
    public sealed class QueryExecutionResult
    {
        /// <summary>
        /// Result sets returned by the batch (one <see cref="DataTable"/> per SELECT).
        /// Empty for statements that return no rows.
        /// </summary>
        public List<DataTable> ResultTables { get; } = new();

        /// <summary>
        /// Combined informational, PRINT and error text produced by the batch,
        /// shown in the Messages tab.
        /// </summary>
        public string Messages { get; set; } = string.Empty;

        /// <summary>
        /// Total number of rows affected by non-query statements in the batch.
        /// </summary>
        public int RecordsAffected { get; set; }

        /// <summary>
        /// Wall-clock time taken to execute the batch.
        /// </summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>
        /// Whether the batch completed without a SQL error.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Total number of rows across all returned result sets.
        /// </summary>
        public int TotalRows
        {
            get
            {
                int total = 0;

                foreach (DataTable table in ResultTables)
                {
                    total += table.Rows.Count;
                }

                return total;
            }
        }
    }
}
