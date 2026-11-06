using System.Data.Common;
using LightSQL.Models;
using Microsoft.Data.SqlClient;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// Microsoft SQL Server provider (Microsoft.Data.SqlClient). Reads metadata from the
    /// <c>sys.*</c> catalog views, surfaces PRINT/warnings via <see cref="SqlConnection.InfoMessage"/>,
    /// and runs server-level work from <c>master</c>.
    /// </summary>
    public sealed class SqlServerDatabaseProvider : DatabaseProviderBase
    {
        private const string ApplicationName = "LightSQL";
        private const int DefaultConnectTimeoutSeconds = 15;

        private readonly SqlServerDialect dialect = new();

        /// <inheritdoc />
        public override DatabaseEngine Engine
        {
            get
            {
                return DatabaseEngine.SqlServer;
            }
        }

        /// <inheritdoc />
        public override string DisplayName
        {
            get
            {
                return "Microsoft SQL Server";
            }
        }

        /// <inheritdoc />
        public override ISqlDialect Dialect
        {
            get
            {
                return dialect;
            }
        }

        /// <inheritdoc />
        public override bool SupportsWindowsAuthentication
        {
            get
            {
                return true;
            }
        }

        /// <inheritdoc />
        public override int DefaultPort
        {
            get
            {
                return 1433;
            }
        }

        /// <inheritdoc />
        public override string? AdministrativeDatabase
        {
            get
            {
                return "master";
            }
        }

        /// <inheritdoc />
        protected override string CurrentDatabaseQuery
        {
            get
            {
                return "SELECT DB_NAME()";
            }
        }

        /// <inheritdoc />
        public override DbConnection CreateConnection(string connectionString)
        {
            return new SqlConnection(connectionString);
        }

        /// <inheritdoc />
        public override string BuildConnectionString(ConnectionProfile profile, string? overrideDatabase)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            SqlConnectionStringBuilder builder;

            if (profile.UseRawConnectionString == true)
            {
                builder = new SqlConnectionStringBuilder(profile.RawConnectionString);

                // Re-inject a decrypted/typed password when the raw string has none.
                if (string.IsNullOrEmpty(profile.Password) == false && string.IsNullOrEmpty(builder.Password) == true)
                {
                    builder.Password = profile.Password;
                }
            }
            else
            {
                // SQL Server takes the port in the data source as "server,port".
                string dataSource = profile.Port.HasValue == true ? $"{profile.Server},{profile.Port.Value}" : profile.Server;

                builder = new SqlConnectionStringBuilder
                {
                    DataSource = dataSource,
                    Encrypt = profile.Encrypt,
                    TrustServerCertificate = profile.TrustServerCertificate,
                    ApplicationName = ApplicationName,
                    ConnectTimeout = DefaultConnectTimeoutSeconds
                };

                if (string.IsNullOrWhiteSpace(profile.Database) == false)
                {
                    builder.InitialCatalog = profile.Database;
                }

                if (profile.AuthenticationType == AuthenticationType.Windows)
                {
                    builder.IntegratedSecurity = true;
                }
                else
                {
                    builder.IntegratedSecurity = false;
                    builder.UserID = profile.UserId;
                    builder.Password = profile.Password ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(overrideDatabase) == false)
            {
                builder.InitialCatalog = overrideDatabase;
            }

            return builder.ConnectionString;
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<string>> GetDatabasesAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sys.databases WHERE state = 0 ORDER BY name";

            return await ReadNamesAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<SchemaObject>> GetTablesAsync(DbConnection connection, string database, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = @"SELECT s.name AS SchemaName, t.name AS ObjectName
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                ORDER BY s.name, t.name";

            return await ReadSchemaObjectsAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<SchemaObject>> GetViewsAsync(DbConnection connection, string database, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = @"SELECT s.name AS SchemaName, v.name AS ObjectName
                FROM sys.views v
                INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
                ORDER BY s.name, v.name";

            return await ReadSchemaObjectsAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(DbConnection connection, string database, string schema, string objectName, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = @"SELECT c.name AS ColumnName, ty.name AS TypeName,
                    c.max_length AS MaxLength, c.precision AS Precision, c.scale AS Scale, c.is_nullable AS IsNullable
                FROM sys.columns c
                INNER JOIN sys.objects o ON c.object_id = o.object_id
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                WHERE s.name = @schema AND o.name = @object
                ORDER BY c.column_id";

            AddParameter(command, "@schema", schema);
            AddParameter(command, "@object", objectName);

            List<ColumnInfo> columns = new();

            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken) == true)
            {
                string typeName = reader.GetString(1);
                short maxLength = reader.GetInt16(2);
                byte precision = reader.GetByte(3);
                byte scale = reader.GetByte(4);

                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = FormatDataType(typeName, maxLength, precision, scale),
                    IsNullable = reader.GetBoolean(5)
                });
            }

            return columns;
        }

        /// <inheritdoc />
        public override void AttachMessageHandler(DbConnection connection, Action<string> onMessage)
        {
            if (connection is SqlConnection sqlConnection)
            {
                sqlConnection.InfoMessage += (sender, args) =>
                {
                    foreach (SqlError error in args.Errors)
                    {
                        onMessage(error.Message);
                    }
                };
            }
        }

        /// <inheritdoc />
        public override IEnumerable<string> GetErrorMessages(Exception exception)
        {
            if (exception is not SqlException sqlException)
            {
                yield break;
            }

            foreach (SqlError error in sqlException.Errors)
            {
                yield return $"Msg {error.Number}, Level {error.Class}, State {error.State}, Line {error.LineNumber}";
                yield return error.Message;
            }
        }

        /// <inheritdoc />
        public override string? ExtractPassword(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString) == true)
            {
                return null;
            }

            try
            {
                SqlConnectionStringBuilder builder = new(connectionString);

                return string.IsNullOrEmpty(builder.Password) == true ? null : builder.Password;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public override string StripPassword(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString) == true)
            {
                return connectionString;
            }

            try
            {
                SqlConnectionStringBuilder builder = new(connectionString);
                builder.Remove("Password");
                builder.Remove("PWD");

                return builder.ConnectionString;
            }
            catch (Exception)
            {
                // If the string cannot be parsed it cannot be used to connect either;
                // return empty rather than risk leaking an embedded password.
                return string.Empty;
            }
        }

        /// <summary>
        /// Formats a SQL Server type name with its length/precision for display.
        /// </summary>
        /// <param name="typeName">The base type name.</param>
        /// <param name="maxLength">The max_length value from sys.columns.</param>
        /// <param name="precision">The numeric precision.</param>
        /// <param name="scale">The numeric scale.</param>
        /// <returns>A display string such as "nvarchar(50)" or "decimal(18,2)".</returns>
        private static string FormatDataType(string typeName, short maxLength, byte precision, byte scale)
        {
            switch (typeName.ToLowerInvariant())
            {
                case "nvarchar":
                case "nchar":
                {
                    // Unicode types store two bytes per character; -1 means MAX.
                    string length = maxLength == -1 ? "max" : (maxLength / 2).ToString();

                    return $"{typeName}({length})";
                }

                case "varchar":
                case "char":
                case "varbinary":
                case "binary":
                {
                    string length = maxLength == -1 ? "max" : maxLength.ToString();

                    return $"{typeName}({length})";
                }

                case "decimal":
                case "numeric":
                {
                    return $"{typeName}({precision},{scale})";
                }

                default:
                {
                    return typeName;
                }
            }
        }
    }
}
