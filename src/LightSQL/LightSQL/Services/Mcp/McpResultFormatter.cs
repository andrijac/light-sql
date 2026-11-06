using System.Data;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LightSQL.Models;

namespace LightSQL.Services.Mcp
{
    /// <summary>
    /// Turns database results into the compact JSON text returned to AI clients by
    /// the MCP tools. JSON keeps column names, types and NULLs unambiguous for a
    /// model, and the row limit keeps a large SELECT from flooding its context.
    /// </summary>
    public static class McpResultFormatter
    {
        /// <summary>
        /// Output options: compact (indentation would cost the model tokens on every
        /// row) and without HTML escaping, so quotes in SQL messages stay readable
        /// ("'Orders'" instead of "'Orders'"). The text is never put in HTML.
        /// </summary>
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// Rows returned per result set when the client asks for no limit.
        /// </summary>
        public const int DefaultMaxRows = 200;

        /// <summary>
        /// The largest row limit a client may ask for.
        /// </summary>
        public const int MaxRowsLimit = 5000;

        /// <summary>
        /// Longest text value returned per cell; longer values are cut.
        /// </summary>
        public const int MaxCellLength = 4000;

        /// <summary>
        /// Keeps a requested row limit within 1..<see cref="MaxRowsLimit"/>.
        /// </summary>
        /// <param name="requested">The limit asked for; zero or less means the default.</param>
        /// <returns>The limit to apply.</returns>
        public static int ClampMaxRows(int requested)
        {
            if (requested <= 0)
            {
                return DefaultMaxRows;
            }

            return Math.Min(requested, MaxRowsLimit);
        }

        /// <summary>
        /// Formats a batch result: success flag, messages, rows affected and every
        /// result set (columns, types and up to <paramref name="maxRows"/> rows).
        /// </summary>
        /// <param name="result">The execution result.</param>
        /// <param name="database">The database the batch ran in.</param>
        /// <param name="maxRows">Rows to include per result set.</param>
        /// <returns>The JSON text.</returns>
        public static string FormatExecutionResult(QueryExecutionResult result, string? database, int maxRows)
        {
            JsonArray resultSets = new();

            foreach (DataTable table in result.ResultTables)
            {
                resultSets.Add(FormatTable(table, maxRows));
            }

            JsonObject root = new()
            {
                ["success"] = result.IsSuccess,
                ["database"] = database,
                ["elapsedMs"] = (long)result.Elapsed.TotalMilliseconds,
                ["recordsAffected"] = result.RecordsAffected,
                ["messages"] = result.Messages,
                ["resultSets"] = resultSets
            };

            if (result.NewDatabaseContext is not null)
            {
                // The MCP server keeps no session, so a USE only lasts for its batch.
                root["note"] = "USE only applies within this batch; pass the 'database' argument to choose a database.";
            }

            return root.ToJsonString(jsonOptions);
        }

        /// <summary>
        /// Formats a list of names as a JSON array.
        /// </summary>
        /// <param name="names">The names.</param>
        /// <returns>The JSON text.</returns>
        public static string FormatNames(IEnumerable<string> names)
        {
            JsonArray array = new();

            foreach (string name in names)
            {
                array.Add(name);
            }

            return array.ToJsonString(jsonOptions);
        }

        /// <summary>
        /// Formats the tables and views of a database.
        /// </summary>
        /// <param name="database">The database that was listed.</param>
        /// <param name="tables">The tables.</param>
        /// <param name="views">The views.</param>
        /// <returns>The JSON text.</returns>
        public static string FormatObjects(string? database, IEnumerable<SchemaObject> tables, IEnumerable<SchemaObject> views)
        {
            JsonArray objects = new();

            foreach (SchemaObject table in tables)
            {
                objects.Add(new JsonObject { ["schema"] = table.Schema, ["name"] = table.Name, ["type"] = "table" });
            }

            foreach (SchemaObject view in views)
            {
                objects.Add(new JsonObject { ["schema"] = view.Schema, ["name"] = view.Name, ["type"] = "view" });
            }

            JsonObject root = new()
            {
                ["database"] = database,
                ["objects"] = objects
            };

            return root.ToJsonString(jsonOptions);
        }

        /// <summary>
        /// Formats the columns of a table or view.
        /// </summary>
        /// <param name="database">The owning database.</param>
        /// <param name="schema">The object's schema.</param>
        /// <param name="objectName">The object name.</param>
        /// <param name="columns">The columns, in ordinal order.</param>
        /// <returns>The JSON text.</returns>
        public static string FormatColumns(string? database, string schema, string objectName, IEnumerable<ColumnInfo> columns)
        {
            JsonArray array = new();

            foreach (ColumnInfo column in columns)
            {
                array.Add(new JsonObject
                {
                    ["name"] = column.Name,
                    ["type"] = column.DataType,
                    ["nullable"] = column.IsNullable
                });
            }

            JsonObject root = new()
            {
                ["database"] = database,
                ["schema"] = schema,
                ["name"] = objectName,
                ["columns"] = array
            };

            return root.ToJsonString(jsonOptions);
        }

        /// <summary>
        /// Converts one cell value to JSON: NULL becomes null, numbers and booleans
        /// stay native, dates use ISO 8601, binary data becomes hex ("0x..."), and
        /// everything else its invariant text (cut at <see cref="MaxCellLength"/>).
        /// </summary>
        /// <param name="value">The cell value.</param>
        /// <returns>The JSON value, or null for SQL NULL.</returns>
        public static JsonNode? ToJsonValue(object? value)
        {
            switch (value)
            {
                case null:
                case DBNull:
                {
                    return null;
                }

                case bool boolean:
                {
                    return JsonValue.Create(boolean);
                }

                case byte or short or int or long:
                {
                    return JsonValue.Create(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                }

                case decimal number:
                {
                    return JsonValue.Create(number);
                }

                case float or double:
                {
                    double real = Convert.ToDouble(value, CultureInfo.InvariantCulture);

                    // JSON has no NaN or infinity; send those as text.
                    if (double.IsFinite(real) == false)
                    {
                        return JsonValue.Create(real.ToString(CultureInfo.InvariantCulture));
                    }

                    return JsonValue.Create(real);
                }

                case DateTime dateTime:
                {
                    return JsonValue.Create(dateTime.ToString("o", CultureInfo.InvariantCulture));
                }

                case DateTimeOffset dateTimeOffset:
                {
                    return JsonValue.Create(dateTimeOffset.ToString("o", CultureInfo.InvariantCulture));
                }

                case byte[] bytes:
                {
                    int shown = Math.Min(bytes.Length, MaxCellLength / 2);
                    string hex = "0x" + Convert.ToHexString(bytes, 0, shown);

                    return JsonValue.Create(shown < bytes.Length ? hex + "..." : hex);
                }

                default:
                {
                    string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

                    return JsonValue.Create(Truncate(text));
                }
            }
        }

        /// <summary>
        /// Formats one result set.
        /// </summary>
        /// <param name="table">The result set.</param>
        /// <param name="maxRows">Rows to include.</param>
        /// <returns>The JSON object for the result set.</returns>
        private static JsonObject FormatTable(DataTable table, int maxRows)
        {
            JsonArray columns = new();

            foreach (DataColumn column in table.Columns)
            {
                columns.Add(new JsonObject { ["name"] = column.ColumnName, ["type"] = column.DataType.Name });
            }

            JsonArray rows = new();
            int shownRows = Math.Min(table.Rows.Count, maxRows);

            for (int rowIndex = 0; rowIndex < shownRows; rowIndex++)
            {
                DataRow row = table.Rows[rowIndex];
                JsonArray cells = new();

                for (int columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
                {
                    cells.Add(ToJsonValue(row[columnIndex]));
                }

                rows.Add(cells);
            }

            return new JsonObject
            {
                ["columns"] = columns,
                ["rows"] = rows,
                ["totalRows"] = table.Rows.Count,
                ["truncated"] = shownRows < table.Rows.Count
            };
        }

        /// <summary>
        /// Cuts a long text value to <see cref="MaxCellLength"/> characters.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The text, or its start followed by "...".</returns>
        private static string Truncate(string text)
        {
            if (text.Length <= MaxCellLength)
            {
                return text;
            }

            return text.Substring(0, MaxCellLength) + "...";
        }
    }
}
