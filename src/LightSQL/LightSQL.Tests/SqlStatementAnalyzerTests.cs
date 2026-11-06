using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the UPDATE/DELETE-without-WHERE detection, including the tricky cases of
    /// WHERE appearing inside strings, comments, identifiers and subqueries.
    /// </summary>
    public class SqlStatementAnalyzerTests
    {
        private readonly SqlStatementAnalyzer analyzer = new();

        [Fact]
        public void UpdateWithoutWhere_IsFlagged()
        {
            Assert.True(analyzer.HasUnguardedUpdateOrDelete("UPDATE Customers SET Active = 1"));
        }

        [Fact]
        public void UpdateWithWhere_IsNotFlagged()
        {
            Assert.False(analyzer.HasUnguardedUpdateOrDelete("UPDATE Customers SET Active = 1 WHERE Id = 5"));
        }

        [Fact]
        public void DeleteWithoutWhere_IsFlagged()
        {
            Assert.True(analyzer.HasUnguardedUpdateOrDelete("DELETE FROM Orders"));
        }

        [Fact]
        public void DeleteWithWhere_IsNotFlagged()
        {
            Assert.False(analyzer.HasUnguardedUpdateOrDelete("DELETE FROM Orders WHERE Total = 0"));
        }

        [Fact]
        public void LowercaseKeywords_AreHandled()
        {
            Assert.False(analyzer.HasUnguardedUpdateOrDelete("update t set x = 1 where id = 2"));
            Assert.True(analyzer.HasUnguardedUpdateOrDelete("delete from t"));
        }

        [Fact]
        public void WhereInsideStringLiteral_DoesNotCount()
        {
            Assert.True(analyzer.HasUnguardedUpdateOrDelete("UPDATE t SET Name = 'contains WHERE word'"));
        }

        [Fact]
        public void WhereInsideLineComment_DoesNotCount()
        {
            Assert.True(analyzer.HasUnguardedUpdateOrDelete("UPDATE t SET x = 1 -- WHERE id = 1"));
        }

        [Fact]
        public void WhereInsideBlockComment_DoesNotCount()
        {
            Assert.True(analyzer.HasUnguardedUpdateOrDelete("UPDATE t SET x = 1 /* WHERE id = 1 */"));
        }

        [Fact]
        public void WhereAsBracketedColumnName_DoesNotCount()
        {
            Assert.True(analyzer.HasUnguardedUpdateOrDelete("UPDATE t SET [WHERE] = 1"));
        }

        [Fact]
        public void WhereAsBacktickColumnName_DoesNotCount()
        {
            // MySQL backtick-quoted `where` is an identifier, not a clause, so the UPDATE is unguarded.
            Assert.True(analyzer.HasUnguardedUpdateOrDelete("UPDATE t SET `where` = 1"));
        }

        [Fact]
        public void WhereOnlyInSubquery_IsStillFlagged()
        {
            string sql = "UPDATE t SET x = (SELECT TOP 1 y FROM z WHERE z.Id = 1)";

            Assert.True(analyzer.HasUnguardedUpdateOrDelete(sql));
        }

        [Fact]
        public void UpdateWithJoinAndWhere_IsNotFlagged()
        {
            string sql = "UPDATE t SET t.x = 1 FROM t INNER JOIN u ON t.id = u.id WHERE u.active = 1";

            Assert.False(analyzer.HasUnguardedUpdateOrDelete(sql));
        }

        [Fact]
        public void MultipleStatements_OneUnguarded_IsFlagged()
        {
            string sql = "UPDATE a SET x = 1 WHERE id = 1; DELETE FROM b;";

            Assert.True(analyzer.HasUnguardedUpdateOrDelete(sql));
        }

        [Fact]
        public void MultipleStatements_AllGuarded_IsNotFlagged()
        {
            string sql = "UPDATE a SET x = 1 WHERE id = 1; DELETE FROM b WHERE id = 2;";

            Assert.False(analyzer.HasUnguardedUpdateOrDelete(sql));
        }

        [Fact]
        public void SelectStatement_IsNotFlagged()
        {
            Assert.False(analyzer.HasUnguardedUpdateOrDelete("SELECT * FROM Customers"));
        }

        [Fact]
        public void EmptyOrWhitespace_IsNotFlagged()
        {
            Assert.False(analyzer.HasUnguardedUpdateOrDelete(string.Empty));
            Assert.False(analyzer.HasUnguardedUpdateOrDelete("   "));
        }

        [Theory]
        [InlineData("SELECT * FROM Orders")]
        [InlineData("select top 10 Id, Name from dbo.Customers where Name like 'A%' order by Name")]
        [InlineData("WITH totals AS (SELECT CustomerId, SUM(Total) AS Total FROM Orders GROUP BY CustomerId) SELECT * FROM totals")]
        [InlineData("DECLARE @from date = '2026-01-01'; SELECT COUNT(*) FROM Orders WHERE OrderDate >= @from")]
        [InlineData("SET NOCOUNT ON; SELECT 1")]
        [InlineData("IF EXISTS (SELECT 1 FROM sys.tables) SELECT name FROM sys.tables ELSE PRINT 'none'")]
        [InlineData("SELECT 'INSERT INTO x; DROP TABLE y' AS Text")]
        [InlineData("-- DELETE FROM Orders\nSELECT 1 /* UPDATE Orders SET x = 1 */")]
        [InlineData("SELECT [Update], [Delete] FROM [Audit]")]
        [InlineData("SELECT LastUpdated, IsDeleted FROM Orders")]
        [InlineData("DECLARE @insert int = 1; SELECT @insert")]
        [InlineData("USE Sales; SELECT * FROM Orders")]
        [InlineData("SELECT `insert`, `delete` FROM `audit`")]
        [InlineData("SHOW TABLES")]
        [InlineData("EXPLAIN SELECT * FROM Orders")]
        [InlineData("")]
        public void FindWriteOperation_AllowsReadingBatches(string sql)
        {
            Assert.Null(analyzer.FindWriteOperation(sql));
        }

        [Theory]
        [InlineData("INSERT INTO Orders (Id) VALUES (1)", "INSERT")]
        [InlineData("SELECT 1; UPDATE Orders SET Total = 0 WHERE Id = 1", "UPDATE")]
        [InlineData("SELECT 1\ndelete from Orders where Id = 1", "DELETE")]
        [InlineData("SELECT 1; MERGE Orders AS t USING Src AS s ON t.Id = s.Id WHEN MATCHED THEN DELETE;", "MERGE")]
        [InlineData("SELECT 1; TRUNCATE TABLE Orders", "TRUNCATE")]
        [InlineData("SELECT * INTO OrdersCopy FROM Orders", "INTO")]
        [InlineData("SELECT 1; DROP TABLE Orders", "DROP")]
        [InlineData("SELECT 1; CREATE TABLE T (Id int)", "CREATE")]
        [InlineData("SELECT 1; ALTER TABLE Orders ADD X int", "ALTER")]
        [InlineData("EXEC sp_who", "EXEC")]
        [InlineData("SELECT 1; EXECUTE ('DELETE FROM Orders')", "EXECUTE")]
        [InlineData("SELECT 1; COMMIT", "COMMIT")]
        [InlineData("BEGIN TRAN; SELECT 1", "TRAN")]
        [InlineData("SELECT 1; GRANT SELECT ON Orders TO public", "GRANT")]
        [InlineData("SELECT * FROM OPENROWSET('SQLNCLI', 'x', 'SELECT 1')", "OPENROWSET")]
        [InlineData("SELECT 1; DBCC CHECKDB", "DBCC")]
        [InlineData("COPY orders TO '/tmp/orders.csv'", "COPY")]
        [InlineData("CALL refresh_totals()", "CALL")]
        [InlineData("SELECT 1; LOCK TABLES orders WRITE", "LOCK")]
        public void FindWriteOperation_BlocksStatementsThatChangeThings(string sql, string expected)
        {
            Assert.Equal(expected, analyzer.FindWriteOperation(sql));
        }

        [Fact]
        public void FindWriteOperation_BlocksABareProcedureCall()
        {
            // T-SQL runs the first statement of a batch as a procedure without EXEC.
            Assert.Equal("procedure call 'sp_configure'", analyzer.FindWriteOperation("sp_configure 'show advanced options', 1"));
            Assert.Equal("procedure call", analyzer.FindWriteOperation("[dbo].[usp_Purge]"));
        }
    }
}
