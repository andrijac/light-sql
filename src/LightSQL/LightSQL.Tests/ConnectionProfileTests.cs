using LightSQL.Models;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests that cloning a connection profile copies every field, including the engine
    /// and port added for multi-engine support.
    /// </summary>
    public class ConnectionProfileTests
    {
        [Fact]
        public void Clone_CopiesEngineAndPort()
        {
            ConnectionProfile original = new()
            {
                Name = "PG",
                Engine = DatabaseEngine.PostgreSql,
                Server = "localhost",
                Port = 5433,
                UserId = "app",
                Password = "pw",
                Database = "sales"
            };

            ConnectionProfile clone = original.Clone();

            Assert.Equal(DatabaseEngine.PostgreSql, clone.Engine);
            Assert.Equal(5433, clone.Port);
            Assert.Equal("localhost", clone.Server);
            Assert.Equal("app", clone.UserId);
            Assert.Equal("sales", clone.Database);
        }

        [Fact]
        public void DefaultEngine_IsSqlServer()
        {
            ConnectionProfile profile = new();

            Assert.Equal(DatabaseEngine.SqlServer, profile.Engine);
            Assert.Null(profile.Port);
        }
    }
}
