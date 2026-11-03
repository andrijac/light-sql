namespace LightSQL.Models
{
    /// <summary>
    /// The kind of node shown in the Object Explorer tree.
    /// Values start at 1 so an unset (default) value is never a valid choice.
    /// </summary>
    public enum DatabaseObjectType
    {
        /// <summary>
        /// The connected server (root of the tree).
        /// </summary>
        Server = 1,

        /// <summary>
        /// The "Databases" grouping folder under the server.
        /// </summary>
        DatabasesFolder = 2,

        /// <summary>
        /// A single database.
        /// </summary>
        Database = 3,

        /// <summary>
        /// The "Tables" grouping folder under a database.
        /// </summary>
        TablesFolder = 4,

        /// <summary>
        /// A single table.
        /// </summary>
        Table = 5,

        /// <summary>
        /// The "Views" grouping folder under a database.
        /// </summary>
        ViewsFolder = 6,

        /// <summary>
        /// A single view.
        /// </summary>
        View = 7,

        /// <summary>
        /// The "Columns" grouping folder under a table or view.
        /// </summary>
        ColumnsFolder = 8,

        /// <summary>
        /// A single column of a table or view.
        /// </summary>
        Column = 9
    }
}
