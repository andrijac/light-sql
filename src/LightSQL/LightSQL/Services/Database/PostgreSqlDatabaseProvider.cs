using System.Data.Common;
using System.Text;
using LightSQL.Models;
using Npgsql;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// PostgreSQL provider (Npgsql). Lists databases from <c>pg_database</c>, reads
    /// tables / views / columns from <c>information_schema</c>, surfaces server notices via
    /// <see cref="NpgsqlConnection.Notice"/>, and runs server-level work from <c>postgres</c>.
    /// </summary>
    public sealed class PostgreSqlDatabaseProvider : DatabaseProviderBase
    {
        private const string ApplicationName = "LightSQL";
        private const int DefaultConnectTimeoutSeconds = 15;

        /// <summary>
        /// Database Npgsql connects to when the profile names none (connecting to a
        /// non-existent, user-named database otherwise fails).
        /// </summary>
        private const string FallbackDatabase = "postgres";

        private readonly PostgreSqlDialect dialect = new();

        /// <inheritdoc />
        public override DatabaseEngine Engine
        {
            get
            {
                return DatabaseEngine.PostgreSql;
            }
        }

        /// <inheritdoc />
        public override string DisplayName
        {
            get
            {
                return "PostgreSQL";
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
                return false;
            }
        }

        /// <inheritdoc />
        public override int DefaultPort
        {
            get
            {
                return 5432;
            }
        }

        /// <inheritdoc />
        public override string? AdministrativeDatabase
        {
            get
            {
                return FallbackDatabase;
            }
        }

        /// <inheritdoc />
        protected override string CurrentDatabaseQuery
        {
            get
            {
                return "SELECT current_database()";
            }
        }

        /// <inheritdoc />
        public override DbConnection CreateConnection(string connectionString)
        {
            return new NpgsqlConnection(connectionString);
        }

        /// <inheritdoc />
        public override string BuildConnectionString(ConnectionProfile profile, string? overrideDatabase)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            NpgsqlConnectionStringBuilder builder;

            if (profile.UseRawConnectionString == true)
            {
                builder = new NpgsqlConnectionStringBuilder(profile.RawConnectionString);

                // Re-inject a decrypted/typed password when the raw string has none.
                if (string.IsNullOrEmpty(profile.Password) == false && string.IsNullOrEmpty(builder.Password) == true)
                {
                    builder.Password = profile.Password;
                }
            }
            else
            {
                builder = new NpgsqlConnectionStringBuilder
                {
                    Host = profile.Server,
                    Port = profile.Port ?? DefaultPort,
                    Username = profile.UserId,
                    Password = profile.Password ?? string.Empty,
                    SslMode = ResolveSslMode(profile),
                    ApplicationName = ApplicationName,
                    Timeout = DefaultConnectTimeoutSeconds
                };

                if (string.IsNullOrWhiteSpace(profile.Database) == false)
                {
                    builder.Database = profile.Database;
                }
            }

            if (string.IsNullOrWhiteSpace(overrideDatabase) == false)
            {
                builder.Database = overrideDatabase;
            }

            // PostgreSQL connects to a specific database; fall back to a known one.
            if (string.IsNullOrWhiteSpace(builder.Database) == true)
            {
                builder.Database = FallbackDatabase;
            }

            return builder.ConnectionString;
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<string>> GetDatabasesAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT datname FROM pg_database WHERE datistemplate = false ORDER BY datname";

            return await ReadNamesAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<SchemaObject>> GetTablesAsync(DbConnection connection, string database, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = @"SELECT table_schema, table_name
                FROM information_schema.tables
                WHERE table_type = 'BASE TABLE'
                    AND table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY table_schema, table_name";

            return await ReadSchemaObjectsAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<SchemaObject>> GetViewsAsync(DbConnection connection, string database, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = @"SELECT table_schema, table_name
                FROM information_schema.views
                WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY table_schema, table_name";

            return await ReadSchemaObjectsAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(DbConnection connection, string database, string schema, string objectName, CancellationToken cancellationToken)
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = @"SELECT column_name, data_type, character_maximum_length,
                    numeric_precision, numeric_scale, is_nullable
                FROM information_schema.columns
                WHERE table_schema = @schema AND table_name = @object
                ORDER BY ordinal_position";

            AddParameter(command, "@schema", string.IsNullOrWhiteSpace(schema) == true ? "public" : schema);
            AddParameter(command, "@object", objectName);

            List<ColumnInfo> columns = new();

            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken) == true)
            {
                string dataType = reader.GetString(1);
                int? maxLength = reader.IsDBNull(2) == true ? null : reader.GetInt32(2);
                int? precision = reader.IsDBNull(3) == true ? null : reader.GetInt32(3);
                int? scale = reader.IsDBNull(4) == true ? null : reader.GetInt32(4);
                bool isNullable = string.Equals(reader.GetString(5), "YES", StringComparison.OrdinalIgnoreCase) == true;

                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = FormatDataType(dataType, maxLength, precision, scale),
                    IsNullable = isNullable
                });
            }

            return columns;
        }

        /// <inheritdoc />
        public override void AttachMessageHandler(DbConnection connection, Action<string> onMessage)
        {
            if (connection is NpgsqlConnection npgsqlConnection)
            {
                npgsqlConnection.Notice += (sender, args) => onMessage(args.Notice.MessageText);
            }
        }

        /// <inheritdoc />
        public override IEnumerable<string> GetErrorMessages(Exception exception)
        {
            if (exception is PostgresException postgresException)
            {
                yield return postgresException.MessageText;

                if (string.IsNullOrWhiteSpace(postgresException.Detail) == false)
                {
                    yield return postgresException.Detail;
                }
            }
            else if (exception is NpgsqlException npgsqlException)
            {
                yield return npgsqlException.Message;
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
                NpgsqlConnectionStringBuilder builder = new(connectionString);

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
                NpgsqlConnectionStringBuilder builder = new(connectionString)
                {
                    Password = null
                };

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
        /// Maps the profile's Encrypt / Trust-certificate choices onto an Npgsql SSL mode:
        /// no encryption, encrypt-and-trust (Prefer), or encrypt-and-verify (VerifyFull).
        /// </summary>
        /// <param name="profile">The connection profile.</param>
        /// <returns>The SSL mode.</returns>
        private static SslMode ResolveSslMode(ConnectionProfile profile)
        {
            if (profile.Encrypt == false)
            {
                return SslMode.Disable;
            }

            // Prefer uses SSL when the server offers it, without validating the certificate;
            // VerifyFull requires SSL and validates the certificate chain and host name.
            return profile.TrustServerCertificate == true ? SslMode.Prefer : SslMode.VerifyFull;
        }

        /// <summary>
        /// Formats a PostgreSQL column type with its length/precision for display, e.g.
        /// "character varying(50)" or "numeric(18,2)".
        /// </summary>
        /// <param name="dataType">The information_schema data_type.</param>
        /// <param name="maxLength">The character maximum length, or null.</param>
        /// <param name="precision">The numeric precision, or null.</param>
        /// <param name="scale">The numeric scale, or null.</param>
        /// <returns>The display type string.</returns>
        private static string FormatDataType(string dataType, int? maxLength, int? precision, int? scale)
        {
            if (maxLength.HasValue == true)
            {
                return $"{dataType}({maxLength.Value})";
            }

            bool isDecimal = string.Equals(dataType, "numeric", StringComparison.OrdinalIgnoreCase) == true
                || string.Equals(dataType, "decimal", StringComparison.OrdinalIgnoreCase) == true;

            if (isDecimal == true && precision.HasValue == true)
            {
                StringBuilder builder = new();
                builder.Append(dataType);
                builder.Append('(');
                builder.Append(precision.Value);

                if (scale.HasValue == true)
                {
                    builder.Append(',');
                    builder.Append(scale.Value);
                }

                builder.Append(')');

                return builder.ToString();
            }

            return dataType;
        }
    }
}
