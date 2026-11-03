namespace LightSQL.Models
{
    /// <summary>
    /// Describes a single column of a table or view.
    /// </summary>
    public sealed class ColumnInfo
    {
        /// <summary>
        /// The column name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The column's SQL data type, including length/precision, e.g. "nvarchar(50)".
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// Whether the column allows NULL values.
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// A display label combining the name, type and nullability.
        /// </summary>
        public string DisplayText
        {
            get
            {
                string nullability = IsNullable == true ? "null" : "not null";

                return $"{Name} ({DataType}, {nullability})";
            }
        }
    }
}
