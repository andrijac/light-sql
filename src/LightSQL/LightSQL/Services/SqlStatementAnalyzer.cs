using System.Text;

namespace LightSQL.Services
{
    /// <summary>
    /// Detects UPDATE / DELETE statements that have no WHERE clause.
    ///
    /// This is a deliberately conservative, lightweight scanner (not a full T-SQL
    /// parser). It strips comments and literals first, then walks the remaining
    /// tokens tracking parenthesis depth so that a WHERE belonging to a subquery is
    /// not mistaken for the outer statement's WHERE. The analysis is biased towards
    /// warning: when in doubt it reports the statement as unguarded, because a missed
    /// warning is more dangerous than an extra one.
    /// </summary>
    public sealed class SqlStatementAnalyzer : ISqlStatementAnalyzer
    {
        /// <summary>
        /// Keywords that start a new statement (or batch). Encountering one at the
        /// same parenthesis level ends the current UPDATE/DELETE statement scan.
        /// Note: SET is intentionally excluded because it is part of UPDATE syntax.
        /// </summary>
        private static readonly HashSet<string> statementBoundaryKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "GO", "INSERT", "UPDATE", "DELETE", "MERGE", "SELECT", "CREATE", "ALTER",
            "DROP", "TRUNCATE", "DECLARE", "BEGIN", "IF", "WHILE", "GRANT", "REVOKE",
            "EXEC", "EXECUTE", "PRINT"
        };

        /// <summary>
        /// Keywords that are not allowed anywhere in a read-only batch, because the
        /// statement they belong to can change data, schema, permissions or server
        /// state, run arbitrary code (EXEC), end the protecting transaction (COMMIT,
        /// ROLLBACK, SAVE TRAN) or reach other servers (OPENROWSET, ...). INTO blocks
        /// SELECT ... INTO, which creates a table.
        /// </summary>
        private static readonly HashSet<string> writeKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "INSERT", "UPDATE", "DELETE", "MERGE", "TRUNCATE", "INTO",
            "CREATE", "ALTER", "DROP", "ENABLE", "DISABLE",
            "GRANT", "REVOKE", "DENY",
            "EXEC", "EXECUTE",
            "COMMIT", "ROLLBACK", "SAVE", "TRAN", "TRANSACTION",
            "BACKUP", "RESTORE", "DBCC", "BULK", "CHECKPOINT", "KILL", "SHUTDOWN", "RECONFIGURE",
            "SETUSER", "WRITETEXT", "UPDATETEXT",
            "OPENROWSET", "OPENQUERY", "OPENDATASOURCE",
            "SEND", "RECEIVE",
            // PostgreSQL / MySQL write or state-changing statements.
            "COPY", "CALL", "LOCK", "VACUUM", "LOAD", "RENAME"
        };

        /// <summary>
        /// Words a read-only batch may start with. Any other first word could be a
        /// procedure call, which T-SQL allows without EXEC as a batch's first statement
        /// (e.g. "sp_configure 'x', 1").
        /// </summary>
        private static readonly HashSet<string> readOnlyBatchStarters = new(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "WITH", "DECLARE", "SET", "PRINT", "IF", "BEGIN", "WHILE", "USE", "RETURN", "RAISERROR", "THROW", "WAITFOR",
            // Read-only introspection in PostgreSQL / MySQL.
            "SHOW", "EXPLAIN", "DESCRIBE", "DESC", "VALUES", "TABLE"
        };

        /// <inheritdoc />
        public bool HasUnguardedUpdateOrDelete(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql) == true)
            {
                return false;
            }

            string cleaned = RemoveCommentsAndLiterals(sql);
            List<string> tokens = Tokenize(cleaned);

            int depth = 0;
            int index = 0;

            while (index < tokens.Count)
            {
                string token = tokens[index];

                if (token == "(")
                {
                    depth++;
                    index++;
                    continue;
                }

                if (token == ")")
                {
                    depth--;
                    index++;
                    continue;
                }

                bool isUpdateOrDelete = string.Equals(token, "UPDATE", StringComparison.OrdinalIgnoreCase) == true
                    || string.Equals(token, "DELETE", StringComparison.OrdinalIgnoreCase) == true;

                if (depth == 0 && isUpdateOrDelete == true)
                {
                    bool hasWhere = StatementHasWhere(tokens, index + 1, out int statementEnd);

                    if (hasWhere == false)
                    {
                        return true;
                    }

                    // Resume scanning from where this statement ended.
                    index = statementEnd;
                    continue;
                }

                index++;
            }

            return false;
        }

        /// <inheritdoc />
        public string? FindWriteOperation(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql) == true)
            {
                return null;
            }

            List<string> words = ExtractWords(RemoveCommentsAndLiterals(sql));

            if (words.Count == 0)
            {
                return null;
            }

            foreach (string word in words)
            {
                if (writeKeywords.Contains(word) == true)
                {
                    return word.ToUpperInvariant();
                }
            }

            // A batch that starts with a name runs it as a stored procedure.
            if (readOnlyBatchStarters.Contains(words[0]) == false)
            {
                // "_id_" is the placeholder of a bracketed/quoted name such as [dbo].[proc].
                return words[0] == "_id_" ? "procedure call" : $"procedure call '{words[0]}'";
            }

            return null;
        }

        /// <summary>
        /// Extracts the words (keywords and plain identifiers) of cleaned SQL.
        /// Variables (@name) and temp-table names (#name) are skipped: they are never
        /// keywords, so "@insert" does not count as INSERT.
        /// </summary>
        /// <param name="sql">SQL with comments and literals neutralized.</param>
        /// <returns>The words, in order.</returns>
        private static List<string> ExtractWords(string sql)
        {
            List<string> words = new();
            int index = 0;

            while (index < sql.Length)
            {
                char current = sql[index];

                if (char.IsLetter(current) == false && current != '_' && current != '@' && current != '#')
                {
                    index++;

                    continue;
                }

                int start = index;

                while (index < sql.Length && (char.IsLetterOrDigit(sql[index]) == true || sql[index] == '_' || sql[index] == '@' || sql[index] == '#' || sql[index] == '$'))
                {
                    index++;
                }

                string word = sql.Substring(start, index - start);

                if (word[0] != '@' && word[0] != '#')
                {
                    words.Add(word);
                }
            }

            return words;
        }

        /// <summary>
        /// Scans the tokens of a single UPDATE/DELETE statement for a WHERE clause at
        /// the statement's own parenthesis level.
        /// </summary>
        /// <param name="tokens">The full token list.</param>
        /// <param name="start">Index just after the UPDATE/DELETE keyword.</param>
        /// <param name="statementEnd">The index where the statement scan stopped.</param>
        /// <returns>True if a WHERE belonging to this statement was found.</returns>
        private static bool StatementHasWhere(List<string> tokens, int start, out int statementEnd)
        {
            int localDepth = 0;
            int index = start;

            while (index < tokens.Count)
            {
                string token = tokens[index];

                if (token == "(")
                {
                    localDepth++;
                    index++;
                    continue;
                }

                if (token == ")")
                {
                    localDepth--;
                    index++;
                    continue;
                }

                if (token == ";")
                {
                    break;
                }

                if (localDepth == 0)
                {
                    if (string.Equals(token, "WHERE", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        statementEnd = index;

                        return true;
                    }

                    if (statementBoundaryKeywords.Contains(token) == true)
                    {
                        break;
                    }
                }

                index++;
            }

            statementEnd = index;

            return false;
        }

        /// <summary>
        /// Replaces comments, string literals and bracketed/quoted identifiers with
        /// harmless placeholders so that keywords appearing inside them are ignored.
        /// </summary>
        /// <param name="sql">The original SQL text.</param>
        /// <returns>SQL with comments and literals neutralized.</returns>
        private static string RemoveCommentsAndLiterals(string sql)
        {
            StringBuilder builder = new(sql.Length);
            int i = 0;
            int length = sql.Length;

            while (i < length)
            {
                char c = sql[i];
                char next = i + 1 < length ? sql[i + 1] : '\0';

                // Line comment: -- ... end of line
                if (c == '-' && next == '-')
                {
                    i += 2;

                    while (i < length && sql[i] != '\n')
                    {
                        i++;
                    }

                    builder.Append(' ');
                    continue;
                }

                // Block comment: /* ... */ with nesting support
                if (c == '/' && next == '*')
                {
                    int commentDepth = 1;
                    i += 2;

                    while (i < length && commentDepth > 0)
                    {
                        if (sql[i] == '/' && i + 1 < length && sql[i + 1] == '*')
                        {
                            commentDepth++;
                            i += 2;
                        }
                        else if (sql[i] == '*' && i + 1 < length && sql[i + 1] == '/')
                        {
                            commentDepth--;
                            i += 2;
                        }
                        else
                        {
                            i++;
                        }
                    }

                    builder.Append(' ');
                    continue;
                }

                // Single-quoted string literal (doubled '' is an escaped quote)
                if (c == '\'')
                {
                    i++;

                    while (i < length)
                    {
                        if (sql[i] == '\'' && i + 1 < length && sql[i + 1] == '\'')
                        {
                            i += 2;
                            continue;
                        }

                        if (sql[i] == '\'')
                        {
                            i++;
                            break;
                        }

                        i++;
                    }

                    // A string literal is a value, not a keyword; collapse to a placeholder.
                    builder.Append(" _lit_ ");
                    continue;
                }

                // Bracketed identifier [ ... ] (doubled ]] is an escaped bracket)
                if (c == '[')
                {
                    i++;

                    while (i < length)
                    {
                        if (sql[i] == ']' && i + 1 < length && sql[i + 1] == ']')
                        {
                            i += 2;
                            continue;
                        }

                        if (sql[i] == ']')
                        {
                            i++;
                            break;
                        }

                        i++;
                    }

                    // A bracketed identifier is a name; replace so [Where] is not a keyword.
                    builder.Append(" _id_ ");
                    continue;
                }

                // Double-quoted identifier " ... "
                if (c == '"')
                {
                    i++;

                    while (i < length && sql[i] != '"')
                    {
                        i++;
                    }

                    if (i < length)
                    {
                        i++;
                    }

                    builder.Append(" _id_ ");
                    continue;
                }

                // Backtick-quoted identifier ` ... ` (MySQL; doubled `` is an escaped backtick)
                if (c == '`')
                {
                    i++;

                    while (i < length)
                    {
                        if (sql[i] == '`' && i + 1 < length && sql[i + 1] == '`')
                        {
                            i += 2;
                            continue;
                        }

                        if (sql[i] == '`')
                        {
                            i++;
                            break;
                        }

                        i++;
                    }

                    // A backtick-quoted identifier is a name; replace so `where` is not a keyword.
                    builder.Append(" _id_ ");
                    continue;
                }

                builder.Append(c);
                i++;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Splits cleaned SQL into word tokens, with parentheses and statement
        /// terminators emitted as their own tokens.
        /// </summary>
        /// <param name="sql">Cleaned SQL text.</param>
        /// <returns>The ordered list of tokens.</returns>
        private static List<string> Tokenize(string sql)
        {
            // Surround structural characters with spaces so they split cleanly.
            StringBuilder spaced = new(sql.Length * 2);

            foreach (char c in sql)
            {
                if (c == '(' || c == ')' || c == ';')
                {
                    spaced.Append(' ');
                    spaced.Append(c);
                    spaced.Append(' ');
                }
                else
                {
                    spaced.Append(c);
                }
            }

            string[] parts = spaced.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            return new List<string>(parts);
        }
    }
}
