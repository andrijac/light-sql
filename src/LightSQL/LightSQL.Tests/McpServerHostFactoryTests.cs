using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// End-to-end tests: starts a real MCP server (Kestrel on 127.0.0.1, port 9000+)
    /// over the fake SQL service and talks to it with the official MCP client, the
    /// same way Claude Code or Codex would.
    /// </summary>
    public class McpServerHostFactoryTests
    {
        private readonly FakeSqlConnectionService sqlService = new();
        private readonly List<McpToolCall> calls = new();

        [Fact]
        public async Task Server_ListensOnLoopbackPortFrom9000_AndStops()
        {
            IMcpServerHost host = await StartAsync();

            try
            {
                Assert.InRange(host.Port, McpPortFinder.FirstPort, McpPortFinder.LastPort);
                Assert.Equal($"http://127.0.0.1:{host.Port}/mcp", host.Endpoint);
                Assert.True(sqlService.IsConnected);
            }
            finally
            {
                await host.StopAsync();
            }

            Assert.True(McpPortFinder.IsLoopbackPortAvailable(host.Port));
        }

        [Fact]
        public async Task TwoServers_GetDifferentPorts()
        {
            IMcpServerHost first = await StartAsync();
            IMcpServerHost second = await StartAsync();

            try
            {
                Assert.NotEqual(first.Port, second.Port);
            }
            finally
            {
                await first.StopAsync();
                await second.StopAsync();
            }
        }

        [Fact]
        public async Task Client_ListsTheTools_AndExecutesAQuery()
        {
            IMcpServerHost host = await StartAsync("Sales");

            try
            {
                await using McpClient client = await McpClient.CreateAsync(
                    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(host.Endpoint) }));

                IList<McpClientTool> tools = await client.ListToolsAsync();

                Assert.Equal(
                    new[] { "describe_table", "execute_query", "list_databases", "list_tables" },
                    tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal));
                Assert.Contains("T-SQL", client.ServerInstructions);

                CallToolResult result = await client.CallToolAsync(
                    "execute_query",
                    new Dictionary<string, object?> { ["sql"] = "SELECT 1 AS Value" });

                Assert.NotEqual(true, result.IsError);
                string text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
                Assert.True(JsonNode.Parse(text)!["success"]!.GetValue<bool>());

                (string Sql, string? Database) batch = Assert.Single(sqlService.ExecutedBatches);
                Assert.Equal("SELECT 1 AS Value", batch.Sql);
                Assert.Equal("Sales", batch.Database);
                Assert.Equal("execute_query", Assert.Single(calls).ToolName);
            }
            finally
            {
                await host.StopAsync();
            }
        }

        [Fact]
        public async Task Client_IsRefusedDataChanges_ByDefault()
        {
            IMcpServerHost host = await StartAsync();

            try
            {
                await using McpClient client = await McpClient.CreateAsync(
                    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(host.Endpoint) }));

                CallToolResult result = await client.CallToolAsync(
                    "execute_query",
                    new Dictionary<string, object?> { ["sql"] = "DELETE FROM Orders WHERE Id = 1" });

                Assert.True(result.IsError);
                Assert.Contains("read-only", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
                Assert.Empty(sqlService.ExecutedBatches);
                Assert.Contains("read-only", client.ServerInstructions);
            }
            finally
            {
                await host.StopAsync();
            }
        }

        [Fact]
        public async Task Server_RejectsBrowserRequestsFromOtherSites()
        {
            IMcpServerHost host = await StartAsync();

            try
            {
                using HttpClient http = new();
                using HttpRequestMessage request = new(HttpMethod.Post, host.Endpoint)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Origin", "https://evil.example.com");

                using HttpResponseMessage response = await http.SendAsync(request);

                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
            finally
            {
                await host.StopAsync();
            }
        }

        [Fact]
        public async Task Start_Fails_WhenTheConnectionFails()
        {
            sqlService.ConnectFailure = new InvalidOperationException("Login failed.");
            McpServerHostFactory factory = new(() => sqlService, new SqlStatementAnalyzer());
            McpServerSettings settings = new() { Profile = new ConnectionProfile { Server = "nowhere" } };

            await Assert.ThrowsAsync<InvalidOperationException>(() => factory.StartAsync(settings, CancellationToken.None));
        }

        /// <summary>
        /// Starts a server over the fake SQL service.
        /// </summary>
        /// <param name="database">The default database, or null.</param>
        /// <returns>The running server.</returns>
        private Task<IMcpServerHost> StartAsync(string? database = null)
        {
            McpServerHostFactory factory = new(() => sqlService, new SqlStatementAnalyzer());

            McpServerSettings settings = new()
            {
                Profile = new ConnectionProfile { Server = "fake-server" },
                DefaultDatabase = database,
                ToolCalled = call =>
                {
                    lock (calls)
                    {
                        calls.Add(call);
                    }
                }
            };

            return factory.StartAsync(settings, CancellationToken.None);
        }
    }
}
