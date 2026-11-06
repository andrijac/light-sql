using LightSQL.Services.Mcp;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the Host / Origin checks that keep web pages from calling the MCP server.
    /// </summary>
    public class McpRequestGuardTests
    {
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("localhost")]
        [InlineData("LOCALHOST")]
        [InlineData("[::1]")]
        public void IsAllowedHost_AcceptsLoopbackNames(string host)
        {
            Assert.True(McpRequestGuard.IsAllowedHost(host));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("evil.example.com")]
        [InlineData("192.168.1.10")]
        [InlineData("localhost.evil.com")]
        public void IsAllowedHost_RejectsOtherNames(string? host)
        {
            Assert.False(McpRequestGuard.IsAllowedHost(host));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("http://localhost:3000")]
        [InlineData("http://127.0.0.1:9000")]
        [InlineData("https://localhost")]
        public void IsAllowedOrigin_AcceptsMissingOrLocalOrigins(string? origin)
        {
            Assert.True(McpRequestGuard.IsAllowedOrigin(origin));
        }

        [Theory]
        [InlineData("https://evil.example.com")]
        [InlineData("http://localhost.evil.com")]
        [InlineData("null")]
        [InlineData("file://localhost/c:/page.html")]
        public void IsAllowedOrigin_RejectsRemoteOrOpaqueOrigins(string origin)
        {
            Assert.False(McpRequestGuard.IsAllowedOrigin(origin));
        }
    }
}
