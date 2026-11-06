using LightSQL.Models;
using LightSQL.Services.Database;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the PostgreSQL dialect: double-quote quoting, LIMIT queries and CREATE / DROP text.
    /// </summary>
    public class PostgreSqlDialectTests
    {
        private readonly PostgreSqlDialect dialect = new();

        [Fact]
        public void Engine_IsPostgreSql()
        {
            Assert.Equal(DatabaseEngine.PostgreSql, dialect.Engine);
        }

        [Fact]
        public void DefaultSchema_IsPublic()
        {
            Assert.Equal("public", dialect.DefaultSchema);
        }

        [Fact]
        public void QuoteIdentifier_WrapsInDoubleQuotes_AndEscapes()
        {
            Assert.Equal("\"Order\"", dialect.QuoteIdentifier("Order"));
            Assert.Equal("\"a\"\"b\"", dialect.QuoteIdentifier("a\"b"));
        }

        [Fact]
        public void QualifyName_WithSchema_QuotesBothParts()
        {
            Assert.Equal("\"public\".\"orders\"", dialect.QualifyName("public", "orders"));
        }

        [Fact]
        public void BuildSelectTop_UsesLimit()
        {
            Assert.Equal("SELECT * FROM \"public\".\"orders\" LIMIT 1000", dialect.BuildSelectTop("public", "orders", 1000));
        }

        [Fact]
        public void BuildCreateDatabase_QuotesName()
        {
            Assert.Equal("CREATE DATABASE \"sales\"", dialect.BuildCreateDatabase("sales"));
        }

        [Fact]
        public void BuildDropStatements_UseQuotedNames()
        {
            Assert.Equal("DROP TABLE \"public\".\"orders\"", dialect.BuildDropTable("public", "orders"));
            Assert.Equal("DROP DATABASE \"sales\"", dialect.BuildDropDatabase("sales"));
        }
    }
}
