using LightSQL.Models;
using LightSQL.Services.Database;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the SQL Server dialect: bracket quoting, TOP queries and CREATE / DROP text.
    /// </summary>
    public class SqlServerDialectTests
    {
        private readonly SqlServerDialect dialect = new();

        [Fact]
        public void Engine_IsSqlServer()
        {
            Assert.Equal(DatabaseEngine.SqlServer, dialect.Engine);
        }

        [Fact]
        public void DefaultSchema_IsDbo()
        {
            Assert.Equal("dbo", dialect.DefaultSchema);
        }

        [Fact]
        public void QuoteIdentifier_WrapsInBrackets_AndEscapesClosingBracket()
        {
            Assert.Equal("[Order Details]", dialect.QuoteIdentifier("Order Details"));
            Assert.Equal("[a]]b]", dialect.QuoteIdentifier("a]b"));
        }

        [Fact]
        public void QualifyName_WithSchema_QuotesBothParts()
        {
            Assert.Equal("[dbo].[Orders]", dialect.QualifyName("dbo", "Orders"));
        }

        [Fact]
        public void QualifyName_WithoutSchema_QuotesNameOnly()
        {
            Assert.Equal("[Orders]", dialect.QualifyName(string.Empty, "Orders"));
            Assert.Equal("[Orders]", dialect.QualifyName(null, "Orders"));
        }

        [Fact]
        public void BuildSelectTop_UsesTop()
        {
            Assert.Equal("SELECT TOP 1000 * FROM [dbo].[Orders]", dialect.BuildSelectTop("dbo", "Orders", 1000));
        }

        [Fact]
        public void BuildCreateDatabase_BracketsName()
        {
            Assert.Equal("CREATE DATABASE [Sales]", dialect.BuildCreateDatabase("Sales"));
        }

        [Fact]
        public void BuildDropStatements_UseBracketedNames()
        {
            Assert.Equal("DROP TABLE [dbo].[Orders]", dialect.BuildDropTable("dbo", "Orders"));
            Assert.Equal("DROP VIEW [dbo].[vOrders]", dialect.BuildDropView("dbo", "vOrders"));
            Assert.Equal("DROP DATABASE [Sales]", dialect.BuildDropDatabase("Sales"));
        }
    }
}
