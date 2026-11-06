using LightSQL.Models;
using LightSQL.ViewModels;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the MCP server tab: starting, stopping, restarting, client setup text,
    /// the activity log and closing.
    /// </summary>
    public class McpServerTabViewModelTests
    {
        private readonly FakeMcpServerHostFactory hostFactory = new();
        private readonly FakeClipboardService clipboardService = new();
        private readonly FakeDialogService dialogService = new();

        [Fact]
        public async Task Start_RunsServer_AndShowsPortAndEndpoint()
        {
            McpServerTabViewModel tab = CreateTab("Sales");

            await tab.StartAsync();

            Assert.Equal(McpServerState.Running, tab.State);
            Assert.True(tab.IsRunning);
            Assert.Equal(9000, tab.Port);
            Assert.Equal("http://127.0.0.1:9000/mcp", tab.Endpoint);
            Assert.NotNull(tab.StartedAt);
            Assert.Equal("MCP SQL01 :9000", tab.Header);
            Assert.Equal("Running", tab.StatusText);
        }

        [Fact]
        public async Task Start_PassesConnectionAndDefaultDatabase()
        {
            McpServerTabViewModel tab = CreateTab("Sales");

            await tab.StartAsync();

            McpServerSettings settings = Assert.Single(hostFactory.StartedSettings);
            Assert.Equal("sql01", settings.Profile.Server);
            Assert.Equal("Sales", settings.DefaultDatabase);
            Assert.NotNull(settings.ToolCalled);
        }

        [Fact]
        public void BlankDatabase_MeansConnectionDefault()
        {
            McpServerTabViewModel tab = CreateTab("  ");

            Assert.Null(tab.DefaultDatabase);
            Assert.Equal("(connection default)", tab.DefaultDatabaseText);
        }

        [Fact]
        public async Task NewServer_IsReadOnly()
        {
            McpServerTabViewModel tab = CreateTab(null);
            await tab.StartAsync();

            Assert.False(tab.IsReadWrite);
            Assert.Equal(McpAccessMode.ReadOnly, tab.AccessMode);
            Assert.Equal(McpAccessMode.ReadOnly, hostFactory.StartedSettings[0].GetAccessMode());
            Assert.StartsWith("Read-only", tab.AccessText);
        }

        [Fact]
        public async Task EnablingReadWrite_AfterConfirmation_AppliesToTheRunningServer()
        {
            dialogService.ConfirmResult = true;
            McpServerTabViewModel tab = CreateTab(null);
            await tab.StartAsync();

            tab.IsReadWrite = true;

            Assert.Equal(1, dialogService.ConfirmCount);
            Assert.True(tab.IsReadWrite);
            Assert.Equal(McpAccessMode.ReadWrite, hostFactory.StartedSettings[0].GetAccessMode());
            Assert.Equal("MCP SQL01 :9000 [read-write]", tab.Header);
            Assert.StartsWith("Read-write", tab.AccessText);
        }

        [Fact]
        public void EnablingReadWrite_Declined_StaysReadOnly_AndResetsTheCheckBox()
        {
            dialogService.ConfirmResult = false;
            McpServerTabViewModel tab = CreateTab(null);
            List<string?> changed = new();
            tab.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            tab.IsReadWrite = true;

            Assert.False(tab.IsReadWrite);
            Assert.Equal(McpAccessMode.ReadOnly, tab.AccessMode);
            Assert.Contains(nameof(McpServerTabViewModel.IsReadWrite), changed);
        }

        [Fact]
        public void DisablingReadWrite_DoesNotAsk()
        {
            dialogService.ConfirmResult = true;
            McpServerTabViewModel tab = CreateTab(null);
            tab.IsReadWrite = true;

            tab.IsReadWrite = false;

            Assert.Equal(1, dialogService.ConfirmCount);
            Assert.Equal(McpAccessMode.ReadOnly, tab.AccessMode);
            Assert.DoesNotContain("read-write", tab.Header);
        }

        [Fact]
        public async Task Restart_KeepsTheAccessMode()
        {
            dialogService.ConfirmResult = true;
            McpServerTabViewModel tab = CreateTab(null);
            await tab.StartAsync();
            tab.IsReadWrite = true;

            await tab.StopAsync();
            await tab.StartAsync();

            Assert.Equal(McpAccessMode.ReadWrite, hostFactory.StartedSettings[1].GetAccessMode());
        }

        [Fact]
        public async Task Start_Failure_IsShownInTheTab()
        {
            hostFactory.StartFailure = new InvalidOperationException("Login failed for user 'x'.");
            McpServerTabViewModel tab = CreateTab(null);

            await tab.StartAsync();

            Assert.Equal(McpServerState.Failed, tab.State);
            Assert.Equal("Failed: Login failed for user 'x'.", tab.StatusText);
            Assert.EndsWith("(failed)", tab.Header);
            Assert.True(tab.StartServerCommand.CanExecute(null));
            Assert.False(tab.StopServerCommand.CanExecute(null));
        }

        [Fact]
        public async Task Stop_StopsHost_AndAllowsRestartOnANewPort()
        {
            McpServerTabViewModel tab = CreateTab(null);
            await tab.StartAsync();

            await tab.StopServerCommand.ExecuteAsync(null);

            Assert.Equal(McpServerState.Stopped, tab.State);
            Assert.Equal(1, hostFactory.Hosts[0].StopCount);
            Assert.Equal("MCP SQL01 :9000 (stopped)", tab.Header);
            Assert.False(tab.CopyEndpointCommand.CanExecute(null));

            await tab.StartServerCommand.ExecuteAsync(null);

            Assert.Equal(McpServerState.Running, tab.State);
            Assert.Equal(9001, tab.Port);
        }

        [Fact]
        public async Task ClientSetup_UsesEndpointAndPort()
        {
            McpServerTabViewModel tab = CreateTab(null);
            await tab.StartAsync();

            Assert.Equal("lightsql-9000", tab.ClientName);
            Assert.Equal("claude mcp add --transport http lightsql-9000 http://127.0.0.1:9000/mcp", tab.ClaudeCommand);
            Assert.Equal($"[mcp_servers.lightsql-9000]{Environment.NewLine}url = \"http://127.0.0.1:9000/mcp\"", tab.CodexConfig);
        }

        [Fact]
        public void ClientSetup_IsEmpty_BeforeTheServerRuns()
        {
            McpServerTabViewModel tab = CreateTab(null);

            Assert.Equal(string.Empty, tab.ClaudeCommand);
            Assert.Equal(string.Empty, tab.CodexConfig);
            Assert.False(tab.CopyClaudeCommandCommand.CanExecute(null));
        }

        [Fact]
        public async Task CopyCommands_PutTextOnTheClipboard()
        {
            McpServerTabViewModel tab = CreateTab(null);
            await tab.StartAsync();

            tab.CopyEndpointCommand.Execute(null);
            Assert.Equal("http://127.0.0.1:9000/mcp", clipboardService.Text);

            tab.CopyClaudeCommandCommand.Execute(null);
            Assert.Equal(tab.ClaudeCommand, clipboardService.Text);

            tab.CopyCodexConfigCommand.Execute(null);
            Assert.Equal(tab.CodexConfig, clipboardService.Text);
            Assert.NotNull(tab.CopyMessage);
        }

        [Fact]
        public async Task ToolCalls_AreLoggedNewestFirst_AndCounted()
        {
            McpServerTabViewModel tab = CreateTab(null);
            await tab.StartAsync();
            Action<McpToolCall> report = hostFactory.StartedSettings[0].ToolCalled!;

            report(new McpToolCall { ToolName = "list_databases" });
            report(new McpToolCall { ToolName = "execute_query" });

            Assert.Equal(2, tab.CallCount);
            Assert.Equal("execute_query", tab.Activity[0].ToolName);
            Assert.Equal("list_databases", tab.Activity[1].ToolName);
        }

        [Fact]
        public void AddActivity_KeepsAtMostTheLimit()
        {
            McpServerTabViewModel tab = CreateTab(null);

            for (int i = 0; i < McpServerTabViewModel.MaxActivityEntries + 5; i++)
            {
                tab.AddActivity(new McpToolCall { ToolName = $"call{i}" });
            }

            Assert.Equal(McpServerTabViewModel.MaxActivityEntries, tab.Activity.Count);
            Assert.Equal($"call{McpServerTabViewModel.MaxActivityEntries + 4}", tab.Activity[0].ToolName);
            Assert.Equal(McpServerTabViewModel.MaxActivityEntries + 5, tab.CallCount);
        }

        [Fact]
        public void ClearActivity_EmptiesTheLog_ButKeepsTheCount()
        {
            McpServerTabViewModel tab = CreateTab(null);
            tab.AddActivity(new McpToolCall { ToolName = "execute_query" });

            tab.ClearActivityCommand.Execute(null);

            Assert.Empty(tab.Activity);
            Assert.Equal(1, tab.CallCount);
        }

        [Fact]
        public async Task OnClosed_StopsTheServer()
        {
            McpServerTabViewModel tab = CreateTab(null);
            await tab.StartAsync();

            tab.OnClosed();

            Assert.Equal(1, hostFactory.Hosts[0].StopCount);
        }

        [Fact]
        public async Task OnClosed_WhileStarting_StopsTheServerOnceStarted()
        {
            hostFactory.StartGate = new TaskCompletionSource();
            McpServerTabViewModel tab = CreateTab(null);
            Task start = tab.StartAsync();

            tab.OnClosed();
            hostFactory.StartGate.SetResult();
            await start;

            Assert.Equal(1, hostFactory.Hosts[0].StopCount);
            Assert.Null(tab.Port);
            Assert.Equal(McpServerState.Stopped, tab.State);
        }

        [Fact]
        public void AuthenticationText_NeverShowsThePassword()
        {
            ConnectionProfile profile = new()
            {
                Server = "sql01",
                AuthenticationType = AuthenticationType.SqlServer,
                UserId = "reporter",
                Password = "s3cret"
            };

            McpServerTabViewModel tab = new(hostFactory, clipboardService, dialogService, profile, "SQL01", null);

            Assert.Equal("SQL Server login 'reporter'", tab.AuthenticationText);
            Assert.DoesNotContain("s3cret", tab.AuthenticationText);
        }

        [Fact]
        public void Profile_IsCopied_SoLaterEditsDoNotAffectTheServer()
        {
            ConnectionProfile profile = new() { Server = "sql01" };
            McpServerTabViewModel tab = new(hostFactory, clipboardService, dialogService, profile, "SQL01", null);

            profile.Server = "other";
            _ = tab.StartAsync();

            Assert.Equal("sql01", hostFactory.StartedSettings[0].Profile.Server);
        }

        /// <summary>
        /// Creates a tab for a Windows-authentication connection to "sql01".
        /// </summary>
        /// <param name="database">The default database, or null.</param>
        /// <returns>The tab (not started).</returns>
        private McpServerTabViewModel CreateTab(string? database)
        {
            ConnectionProfile profile = new() { Server = "sql01" };

            return new McpServerTabViewModel(hostFactory, clipboardService, dialogService, profile, "SQL01", database);
        }
    }
}
