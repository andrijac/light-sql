namespace LightSQL.Services.Tools
{
    /// <summary>
    /// Builds the instructions (system prompt) that tell an AI client how to use the database
    /// tools: the engine and dialect, the single locked database, the read-only default, and
    /// any pasted metadata. Shared by the MCP server and the in-app chat so both behave the
    /// same and benefit from the same database metadata.
    /// </summary>
    public static class DatabaseAssistantPrompt
    {
        /// <summary>
        /// Builds the base instructions for a connection and its single database.
        /// </summary>
        /// <param name="engineName">The engine display name (e.g. "PostgreSQL").</param>
        /// <param name="serverName">The server / host name.</param>
        /// <param name="defaultDatabase">The single database in context.</param>
        /// <returns>The instructions text.</returns>
        public static string BuildInstructions(string engineName, string? serverName, string? defaultDatabase)
        {
            return $"Gives access to the {engineName} server '{serverName}' through LightSQL. "
                + $"Queries use the {engineName} SQL dialect. This server is locked to the single database '{defaultDatabase}' and can only access that database; omit the 'database' argument (naming a different database is rejected). "
                + "Use list_tables and describe_table to explore the schema before writing queries. "
                + "The server is read-only by default: execute_query then rejects statements that change anything and rolls every batch back. "
                + "If the user granted write-access, execute_query can change data, so only run INSERT, UPDATE, DELETE or DDL when the user asked for it. "
                + "Every call runs on a fresh connection, so temporary tables, session settings and USE do not persist between calls.";
        }

        /// <summary>
        /// Appends the user's pasted database metadata (schema and semantics) to the
        /// instructions, when there is any. The document is appended verbatim and is never
        /// parsed, so it can be JSON, Markdown or any text the model can read.
        /// </summary>
        /// <param name="instructions">The base instructions.</param>
        /// <param name="metadata">The metadata document (JSON, Markdown or any text), or null/empty.</param>
        /// <returns>The instructions, with the metadata appended when present.</returns>
        public static string AppendMetadata(string instructions, string? metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata) == true)
            {
                return instructions;
            }

            return instructions
                + "\n\n## Database metadata (authoritative schema and semantics)\n"
                + "The following document, provided by the user in LightSQL, describes this database's tables, "
                + "columns, keys, relationships and meanings. Use it to write correct, well-filtered queries.\n"
                + metadata;
        }

        /// <summary>
        /// Builds the full system prompt: the base instructions plus any pasted metadata.
        /// </summary>
        /// <param name="engineName">The engine display name.</param>
        /// <param name="serverName">The server / host name.</param>
        /// <param name="defaultDatabase">The single database in context.</param>
        /// <param name="metadata">The metadata document (JSON, Markdown or any text), or null/empty.</param>
        /// <returns>The full system prompt.</returns>
        public static string Build(string engineName, string? serverName, string? defaultDatabase, string? metadata)
        {
            return AppendMetadata(BuildInstructions(engineName, serverName, defaultDatabase), metadata);
        }
    }
}
