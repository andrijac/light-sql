using LightSQL.Models;
using LightSQL.Services;
using LightSQL.ViewModels;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests how <see cref="MainViewModel"/> opens MCP server tabs next to the query
    /// tabs, and how query-only commands behave while such a tab is selected.
    /// </summary>
    public class MainViewModelMcpTests
    {
        private readonly FakeSqlConnectionService sqlService = new();
        private readonly FakeDialogService dialogService = new();
        private readonly FakeMcpServerHostFactory mcpHostFactory = new();
        private readonly FakeMetadataStore metadataStore = new();

        [Fact]
        public void StartMcpServer_IsDisabled_WhenNotConnected()
        {
            MainViewModel viewModel = CreateViewModel();

            Assert.False(viewModel.StartMcpServerCommand.CanExecute(null));
        }

        [Fact]
        public async Task StartMcpServer_IsEnabled_WhenConnectedWithDatabaseSelected()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            viewModel.SelectedQueryTab!.ActiveDatabase = "Sales";

            Assert.True(viewModel.StartMcpServerCommand.CanExecute(null));
        }

        [Fact]
        public async Task StartMcpServer_IsDisabled_WhenNoDatabaseSelected()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            viewModel.SelectedQueryTab!.ActiveDatabase = null;

            Assert.False(viewModel.StartMcpServerCommand.CanExecute(null));
        }

        [Fact]
        public async Task StartMcpServer_OpensSelectedRunningTab_WithTheSelectedTabDatabase()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            viewModel.SelectedQueryTab!.ActiveDatabase = "Sales";

            await viewModel.StartMcpServerCommand.ExecuteAsync(null);

            McpServerTabViewModel tab = Assert.Single(viewModel.McpServerTabs);
            Assert.Same(tab, viewModel.SelectedTab);
            Assert.True(tab.IsSelected);
            Assert.Equal(McpServerState.Running, tab.State);
            Assert.Equal("Sales", tab.DefaultDatabase);
            Assert.Equal("fake-server", mcpHostFactory.StartedSettings[0].Profile.Server);
            Assert.Equal(2, viewModel.Tabs.Count);
            Assert.Contains("http://127.0.0.1:9000/mcp", viewModel.StatusMessage);
        }

        [Fact]
        public async Task EachServer_GetsItsOwnTabAndPort()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();

            await viewModel.StartMcpServerCommand.ExecuteAsync(null);
            await viewModel.StartMcpServerCommand.ExecuteAsync(null);

            Assert.Equal(new int?[] { 9000, 9001 }, viewModel.McpServerTabs.Select(tab => tab.Port));
        }

        [Fact]
        public async Task SelectingAnMcpTab_DisablesQueryCommands()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();

            await viewModel.StartMcpServerCommand.ExecuteAsync(null);

            Assert.Null(viewModel.SelectedQueryTab);
            Assert.False(viewModel.IsQueryTabSelected);
            Assert.False(viewModel.ExecuteQueryCommand.CanExecute(null));
            Assert.False(viewModel.SaveFileCommand.CanExecute(null));
            Assert.False(viewModel.CancelCommand.CanExecute(null));

            viewModel.SelectedTab = viewModel.Tabs[0];

            Assert.True(viewModel.IsQueryTabSelected);
            Assert.True(viewModel.ExecuteQueryCommand.CanExecute(null));
        }

        [Fact]
        public async Task ClosingAnMcpTab_StopsItsServer_WithoutPrompting()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            await viewModel.StartMcpServerCommand.ExecuteAsync(null);
            McpServerTabViewModel tab = viewModel.McpServerTabs.Single();

            viewModel.CloseQueryCommand.Execute(tab);

            Assert.Empty(viewModel.McpServerTabs);
            Assert.Equal(1, mcpHostFactory.Hosts[0].StopCount);
            Assert.Empty(dialogService.SaveChangesPrompts);
        }

        [Fact]
        public async Task CloseAllButThis_ClosesMcpTabsToo()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            await viewModel.StartMcpServerCommand.ExecuteAsync(null);

            viewModel.CloseOtherQueriesCommand.Execute(viewModel.Tabs[0]);

            Assert.Single(viewModel.Tabs);
            Assert.Equal(1, mcpHostFactory.Hosts[0].StopCount);
        }

        [Fact]
        public async Task NewQuery_FromAnMcpTab_UsesTheConnectionDatabase()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            await viewModel.StartMcpServerCommand.ExecuteAsync(null);

            viewModel.NewQueryCommand.Execute(null);

            Assert.Equal("master", viewModel.SelectedQueryTab!.ActiveDatabase);
        }

        [Fact]
        public async Task Disconnect_KeepsMcpServersRunning()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            await viewModel.StartMcpServerCommand.ExecuteAsync(null);

            viewModel.DisconnectCommand.Execute(null);

            Assert.Equal(McpServerState.Running, viewModel.McpServerTabs.Single().State);
            Assert.False(viewModel.StartMcpServerCommand.CanExecute(null));
        }

        [Fact]
        public async Task OpenMcpServerTab_WhenNotConnected_ShowsMessage()
        {
            MainViewModel viewModel = CreateViewModel();

            await viewModel.OpenMcpServerTabAsync("Sales");

            Assert.Empty(viewModel.McpServerTabs);
            Assert.Equal(1, dialogService.InformationCount);
        }

        [Fact]
        public async Task ObjectExplorer_StartsServerForTheClickedDatabase()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            TreeNodeViewModel databaseNode = new("HR", DatabaseObjectType.Database) { DatabaseName = "HR" };

            await viewModel.ObjectExplorer.StartMcpServerCommand.ExecuteAsync(databaseNode);

            Assert.Equal("HR", viewModel.McpServerTabs.Single().DefaultDatabase);
        }

        [Fact]
        public async Task ObjectExplorer_ServerNode_DoesNotStartServer()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            TreeNodeViewModel serverNode = new("fake-server", DatabaseObjectType.Server);

            // Starting an MCP server is only allowed on a database node now.
            await viewModel.ObjectExplorer.StartMcpServerCommand.ExecuteAsync(serverNode);

            Assert.Empty(viewModel.McpServerTabs);
        }

        [Fact]
        public async Task EditDatabaseMetadata_OpensDialogForTheSelectedDatabase()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            viewModel.SelectedQueryTab!.ActiveDatabase = "Sales";
            dialogService.MetadataDialogResult = true;

            viewModel.EditDatabaseMetadataCommand.Execute(null);

            Assert.Equal(1, dialogService.MetadataDialogCount);
            Assert.Equal("Sales", dialogService.MetadataDialogDatabase);
            Assert.Equal("Database metadata saved", viewModel.StatusMessage);
        }

        [Fact]
        public void ShowMetadataPrompt_OpensThePromptDialog()
        {
            MainViewModel viewModel = CreateViewModel();

            viewModel.ShowMetadataPromptCommand.Execute(null);

            Assert.Equal(1, dialogService.MetadataPromptDialogCount);
        }

        [Fact]
        public async Task StopAllMcpServers_StopsEveryServer()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            await viewModel.StartMcpServerCommand.ExecuteAsync(null);
            await viewModel.StartMcpServerCommand.ExecuteAsync(null);

            await viewModel.StopAllMcpServersAsync();

            Assert.All(mcpHostFactory.Hosts, host => Assert.Equal(1, host.StopCount));
            Assert.All(viewModel.McpServerTabs, tab => Assert.Equal(McpServerState.Stopped, tab.State));
        }

        [Fact]
        public async Task ConfirmCloseAll_IgnoresMcpTabs()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            await viewModel.StartMcpServerCommand.ExecuteAsync(null);

            Assert.True(viewModel.ConfirmCloseAll());
            Assert.Empty(dialogService.SaveChangesPrompts);
        }

        /// <summary>
        /// Creates a main view model wired to the fakes.
        /// </summary>
        /// <returns>The view model (one empty query tab).</returns>
        private MainViewModel CreateViewModel()
        {
            return new MainViewModel(
                sqlService,
                new SqlStatementAnalyzer(),
                new FakeFileService(),
                new FakeOptionsService(),
                dialogService,
                new ObjectExplorerViewModel(sqlService, dialogService),
                mcpHostFactory,
                new FakeClipboardService(),
                metadataStore,
                new LightSQL.Services.Database.DatabaseProviderRegistry(),
                new FakeLlmClientFactory());
        }

        /// <summary>
        /// Creates a main view model connected to "fake-server".
        /// </summary>
        /// <returns>The connected view model.</returns>
        private async Task<MainViewModel> CreateConnectedViewModelAsync()
        {
            MainViewModel viewModel = CreateViewModel();
            dialogService.ConnectionProfileResult = new ConnectionProfile { Server = "fake-server" };

            await viewModel.ConnectCommand.ExecuteAsync(null);

            return viewModel;
        }
    }
}
