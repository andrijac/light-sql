using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the server-independent parts of <see cref="SqlConnectionService"/>:
    /// detecting that a batch switched its database (USE).
    /// </summary>
    public class SqlConnectionServiceTests
    {
        [Fact]
        public void GetNewDatabaseContext_DatabaseChanged_ReturnsNewDatabase()
        {
            Assert.Equal("Sales", SqlConnectionService.GetNewDatabaseContext("master", "Sales"));
        }

        [Theory]
        [InlineData("master", "master")]
        [InlineData("Sales", "sales")]
        public void GetNewDatabaseContext_SameDatabase_ReturnsNull(string start, string end)
        {
            Assert.Null(SqlConnectionService.GetNewDatabaseContext(start, end));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void GetNewDatabaseContext_NoEndDatabase_ReturnsNull(string? end)
        {
            Assert.Null(SqlConnectionService.GetNewDatabaseContext("master", end));
        }
    }
}
