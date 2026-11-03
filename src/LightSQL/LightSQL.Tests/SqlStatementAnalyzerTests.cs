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
    }
}
