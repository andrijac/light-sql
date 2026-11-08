using System.Reflection;
using LightSQL.Services.Mcp;
using LightSQL.Services.Tools;
using ModelContextProtocol.Server;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Asserts the chat tool catalog stays in sync with the MCP tool surface, so the two
    /// never drift (same tool names and the same parameters).
    /// </summary>
    public class DatabaseToolCatalogTests
    {
        [Fact]
        public void Catalog_MatchesTheMcpToolSurface()
        {
            Dictionary<string, HashSet<string>> mcpTools = typeof(McpDatabaseTools)
                .GetMethods()
                .Select(method => new { method, attribute = method.GetCustomAttribute<McpServerToolAttribute>() })
                .Where(entry => entry.attribute is not null)
                .ToDictionary(
                    entry => entry.attribute!.Name!,
                    entry => entry.method.GetParameters()
                        .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
                        .Select(parameter => parameter.Name!)
                        .ToHashSet());

            Dictionary<string, HashSet<string>> catalog = DatabaseToolCatalog.Tools
                .ToDictionary(tool => tool.Name, tool => tool.Parameters.Select(parameter => parameter.Name).ToHashSet());

            Assert.Equal(mcpTools.Keys.OrderBy(name => name), catalog.Keys.OrderBy(name => name));

            foreach (string name in catalog.Keys)
            {
                Assert.Equal(mcpTools[name], catalog[name]);
            }
        }
    }
}
