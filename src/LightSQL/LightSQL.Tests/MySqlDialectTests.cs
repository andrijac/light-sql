using LightSQL.Models;
using LightSQL.Services.Database;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the MySQL dialect: backtick quoting, LIMIT queries, no schema layer.
    /// </summary>
    public class MySqlDialectTests
    {
        private readonly MySqlDialect dialect = new();

        [Fact]
        public void Engine_IsMySql()
        {
            Assert.Equal(DatabaseEngine.MySql, dialect.Engine);
        }

        [Fact]
        public void DefaultSchema_IsNull()
        {
            Assert.Null(dialect.DefaultSchema);
        }

        [Fact]
        public void QuoteIdentifier_WrapsInBackticks_AndEscapes()
        {
            Assert.Equal("`order`", dialect.QuoteIdentifier("order"));
            Assert.Equal("`a``b`", dialect.QuoteIdentifier("a`b"));
        }

        [Fact]
        public void QualifyName_IgnoresSchema()
        {
            Assert.Equal("`orders`", dialect.QualifyName("anything", "orders"));
            Assert.Equal("`orders`", dialect.QualifyName(null, "orders"));
        }

        [Fact]
        public void BuildSelectTop_UsesLimit_AndNoSchema()
        {
            Assert.Equal("SELECT * FROM `orders` LIMIT 1000", dialect.BuildSelectTop(string.Empty, "orders", 1000));
        }

        [Fact]
        public void BuildCreateDatabase_QuotesName()
        {
            Assert.Equal("CREATE DATABASE `sales`", dialect.BuildCreateDatabase("sales"));
        }

        [Fact]
        public void BuildDropStatements_UseBacktickNames_WithoutSchema()
        {
            Assert.Equal("DROP TABLE `orders`", dialect.BuildDropTable("ignored", "orders"));
            Assert.Equal("DROP DATABASE `sales`", dialect.BuildDropDatabase("sales"));
        }
    }
}
