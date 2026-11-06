using System.IO;
using System.Net;
using LightSQL.Models;
using LightSQL.Services.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace LightSQL.Services.Mcp
{
    /// <summary>
    /// Starts MCP servers in-process with ASP.NET Core (Kestrel) and the official
    /// MCP C# SDK. Each server:
    /// <list type="bullet">
    /// <item>opens its own <see cref="ISqlConnectionService"/> for the connection, so
    /// it keeps working when the main window disconnects or connects elsewhere;</item>
    /// <item>listens only on 127.0.0.1, on the first free port from 9000 up;</item>
    /// <item>uses the stateless Streamable HTTP transport at <see cref="EndpointPath"/>;</item>
    /// <item>rejects requests whose Host / Origin headers are not local (see
    /// <see cref="McpRequestGuard"/>).</item>
    /// </list>
    /// </summary>
    public sealed class McpServerHostFactory : IMcpServerHostFactory
    {
        private readonly Func<ISqlConnectionService> sqlServiceFactory;
        private readonly ISqlStatementAnalyzer statementAnalyzer;
        private readonly IDatabaseProviderRegistry providerRegistry;

        /// <summary>
        /// The URL path of the MCP endpoint.
        /// </summary>
        public const string EndpointPath = "/mcp";

        /// <summary>
        /// How many ports are tried when one turns out to be taken while binding
        /// (another program can grab it between the check and the bind).
        /// </summary>
        private const int MaxBindAttempts = 10;

        /// <summary>
        /// Creates the factory.
        /// </summary>
        /// <param name="sqlServiceFactory">Creates a new, unconnected SQL service for each server.</param>
        /// <param name="statementAnalyzer">Detects statements that could change data (read-only mode).</param>
        /// <param name="providerRegistry">Resolves the engine provider for the connection's instructions.</param>
        public McpServerHostFactory(Func<ISqlConnectionService> sqlServiceFactory, ISqlStatementAnalyzer statementAnalyzer, IDatabaseProviderRegistry providerRegistry)
        {
            this.sqlServiceFactory = sqlServiceFactory;
            this.statementAnalyzer = statementAnalyzer;
            this.providerRegistry = providerRegistry;
        }

        /// <inheritdoc />
        public async Task<IMcpServerHost> StartAsync(McpServerSettings settings, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(settings);

            // Connecting first validates the credentials before a port is taken.
            ISqlConnectionService sqlService = sqlServiceFactory();
            await sqlService.ConnectAsync(settings.Profile, cancellationToken).ConfigureAwait(false);

            McpDatabaseTools tools = new(sqlService, statementAnalyzer, settings.DefaultDatabase, settings.GetAccessMode, settings.ToolCalled);
            IDatabaseProvider provider = providerRegistry.Get(settings.Profile.Engine);
            string instructions = BuildInstructions(provider.DisplayName, sqlService.ServerName, tools.DefaultDatabase);

            HashSet<int> failedPorts = new();

            for (int attempt = 1; attempt <= MaxBindAttempts; attempt++)
            {
                int? port = McpPortFinder.FindAvailablePort(
                    McpPortFinder.FirstPort,
                    McpPortFinder.LastPort,
                    McpPortFinder.IsLoopbackPortAvailable,
                    failedPorts);

                if (port is null)
                {
                    break;
                }

                WebApplication application = BuildApplication(port.Value, tools, instructions);

                try
                {
                    await application.StartAsync(cancellationToken).ConfigureAwait(false);

                    return new McpServerHost(application, port.Value);
                }
                catch (IOException)
                {
                    // Kestrel reports "address already in use" as an IOException; try the next port.
                    failedPorts.Add(port.Value);
                    await application.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    await application.DisposeAsync().ConfigureAwait(false);

                    throw;
                }
            }

            throw new InvalidOperationException(
                $"No free port was found between {McpPortFinder.FirstPort} and {McpPortFinder.LastPort}.");
        }

        /// <summary>
        /// Builds the instructions an AI client receives when it connects, describing
        /// the server and how the tools behave.
        /// </summary>
        /// <param name="engineName">The database engine display name (e.g. "PostgreSQL").</param>
        /// <param name="serverName">The server / host name.</param>
        /// <param name="defaultDatabase">The default database.</param>
        /// <returns>The instructions text.</returns>
        public static string BuildInstructions(string engineName, string? serverName, string? defaultDatabase)
        {
            return $"Gives access to the {engineName} server '{serverName}' through LightSQL. "
                + $"Queries use the {engineName} SQL dialect. The default database is '{defaultDatabase}'; pass 'database' to use another one. "
                + "Use list_databases, list_tables and describe_table to explore the schema before writing queries. "
                + "The server is read-only by default: execute_query then rejects statements that change anything and rolls every batch back. "
                + "If the user switched it to read-write, execute_query can change data, so only run INSERT, UPDATE, DELETE or DDL when the user asked for it. "
                + "Every call runs on a fresh connection, so temporary tables, session settings and USE do not persist between calls.";
        }

        /// <summary>
        /// Builds (but does not start) the web application for one server.
        /// </summary>
        /// <param name="port">The loopback port to listen on.</param>
        /// <param name="tools">The tool set to expose.</param>
        /// <param name="instructions">The server instructions for AI clients.</param>
        /// <returns>The configured application.</returns>
        private static WebApplication BuildApplication(int port, McpDatabaseTools tools, string instructions)
        {
            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory
            });

            // A desktop app has no console; keep the web host quiet.
            builder.Logging.ClearProviders();

            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));

            builder.Services
                .AddMcpServer(options =>
                {
                    options.ServerInfo = new Implementation { Name = "lightsql", Version = "1.0.0" };
                    options.ServerInstructions = instructions;
                })
                .WithHttpTransport(options => options.Stateless = true)
                .WithTools(tools);

            WebApplication application = builder.Build();

            // Only local, non-browser callers may reach the endpoint.
            application.Use(async (HttpContext context, RequestDelegate next) =>
            {
                bool allowed = McpRequestGuard.IsAllowedHost(context.Request.Host.Host) == true
                    && McpRequestGuard.IsAllowedOrigin(context.Request.Headers.Origin.ToString()) == true;

                if (allowed == false)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;

                    return;
                }

                await next(context);
            });

            application.MapMcp(EndpointPath);

            return application;
        }
    }
}
