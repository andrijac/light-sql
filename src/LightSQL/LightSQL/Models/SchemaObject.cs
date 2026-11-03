namespace LightSQL.Models
{
    /// <summary>
    /// A schema-qualified database object such as a table or view.
    /// </summary>
    public sealed class SchemaObject
    {
        /// <summary>
        /// The owning schema name (for example "dbo").
        /// </summary>
        public string Schema { get; set; } = string.Empty;

        /// <summary>
        /// The object name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The fully qualified "schema.name" display text.
        /// </summary>
        public string QualifiedName
        {
            get
            {
                return $"{Schema}.{Name}";
            }
        }
    }
}
