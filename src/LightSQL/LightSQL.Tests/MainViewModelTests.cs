using System.Text;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.ViewModels;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the SSMS-style query tabs managed by <see cref="MainViewModel"/>: opening,
    /// closing (with save prompts), File Open / Save / Save As, per-tab databases and
    /// independent execution.
    /// </summary>
    public class MainViewModelTests
    {
        private readonly FakeSqlConnectionService sqlService = new();
        private readonly FakeDialogService dialogService = new();
        private readonly FakeFileService fileService = new();
        private readonly FakeOptionsService optionsService = new();
        private readonly FakeMcpServerHostFactory mcpHostFactory = new();
        private readonly FakeClipboardService clipboardService = new();

        [Fact]
        public void Startup_OpensOneSelectedEmptyTab()
        {
            MainViewModel viewModel = CreateViewModel();

            QueryTabViewModel tab = Assert.Single(viewModel.QueryTabs);
            Assert.Same(tab, viewModel.SelectedTab);
            Assert.True(tab.IsSelected);
            Assert.Equal("SQLQuery1", tab.Header);
            Assert.True(viewModel.HasTabs);
        }

        [Fact]
        public void NewQuery_AddsAndSelectsTab_WithNextTitle()
        {
            MainViewModel viewModel = CreateViewModel();
            QueryTabViewModel first = QueryTab(viewModel, 0);

            viewModel.NewQueryCommand.Execute(null);

            Assert.Equal(2, viewModel.Tabs.Count);
            QueryTabViewModel second = QueryTab(viewModel, 1);
            Assert.Same(second, viewModel.SelectedTab);
            Assert.Equal("SQLQuery2", second.Header);
            Assert.True(second.IsSelected);
            Assert.False(first.IsSelected);
        }

        [Fact]
        public void NewQuery_TitlesAreNotReused_AfterClosing()
        {
            MainViewModel viewModel = CreateViewModel();
            viewModel.NewQueryCommand.Execute(null);
            viewModel.CloseQueryCommand.Execute(QueryTab(viewModel, 1));

            viewModel.NewQueryCommand.Execute(null);

            Assert.Equal("SQLQuery3", QueryTab(viewModel, 1).Header);
        }

        [Fact]
        public async Task NewQuery_UsesSelectedTabDatabase()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            viewModel.SelectedQueryTab!.ActiveDatabase = "Sales";

            viewModel.NewQueryCommand.Execute(null);

            Assert.Equal("Sales", viewModel.SelectedQueryTab!.ActiveDatabase);
        }

        [Fact]
        public void CloseQuery_UnmodifiedTab_ClosesWithoutPrompt_AndSelectsNeighbour()
        {
            MainViewModel viewModel = CreateViewModel();
            viewModel.NewQueryCommand.Execute(null);
            viewModel.NewQueryCommand.Execute(null);
            QueryTabViewModel middle = QueryTab(viewModel, 1);
            QueryTabViewModel last = QueryTab(viewModel, 2);
            viewModel.SelectedTab = middle;

            viewModel.CloseQueryCommand.Execute(null);

            Assert.Empty(dialogService.SaveChangesPrompts);
            Assert.DoesNotContain(middle, viewModel.Tabs);
            Assert.Same(last, viewModel.SelectedTab);
            Assert.True(last.IsSelected);
        }

        [Fact]
        public void CloseQuery_ModifiedTab_Cancel_KeepsTab()
        {
            MainViewModel viewModel = CreateViewModel();
            QueryTabViewModel tab = QueryTab(viewModel, 0);
            tab.EditorText = "SELECT 1";
            dialogService.SaveChangesResult = SaveChangesChoice.Cancel;

            viewModel.CloseQueryCommand.Execute(tab);

            Assert.Equal(new[] { "SQLQuery1" }, dialogService.SaveChangesPrompts);
            Assert.Contains(tab, viewModel.Tabs);
        }

        [Fact]
        public void CloseQuery_ModifiedTab_Discard_ClosesWithoutSaving()
        {
            MainViewModel viewModel = CreateViewModel();
            QueryTabViewModel tab = QueryTab(viewModel, 0);
            tab.EditorText = "SELECT 1";
            dialogService.SaveChangesResult = SaveChangesChoice.Discard;

            viewModel.CloseQueryCommand.Execute(tab);

            Assert.Empty(viewModel.Tabs);
            Assert.Empty(fileService.Files);
        }

        [Fact]
        public void CloseQuery_ModifiedNewTab_Save_AsksForFileThenCloses()
        {
            MainViewModel viewModel = CreateViewModel();
            QueryTabViewModel tab = QueryTab(viewModel, 0);
            tab.EditorText = "SELECT 42";
            dialogService.SaveChangesResult = SaveChangesChoice.Save;
            dialogService.SaveFileResult = @"C:\Scripts\answer.sql";

            viewModel.CloseQueryCommand.Execute(tab);

            Assert.Empty(viewModel.Tabs);
            Assert.Equal("SELECT 42", fileService.Files[@"C:\Scripts\answer.sql"].Text);
        }

        [Fact]
        public void CloseQuery_ModifiedTab_SaveAsCancelled_KeepsTab()
        {
            MainViewModel viewModel = CreateViewModel();
            QueryTabViewModel tab = QueryTab(viewModel, 0);
            tab.EditorText = "SELECT 42";
            dialogService.SaveChangesResult = SaveChangesChoice.Save;
            dialogService.SaveFileResult = null;

            viewModel.CloseQueryCommand.Execute(tab);

            Assert.Contains(tab, viewModel.Tabs);
            Assert.True(tab.IsModified);
        }

        [Fact]
        public void CloseQuery_ModifiedFile_PromptUsesFileName()
        {
            fileService.AddFile(@"C:\Scripts\report.sql", "SELECT 1");
            MainViewModel viewModel = CreateViewModel();
            viewModel.OpenFiles(new[] { @"C:\Scripts\report.sql" });
            viewModel.SelectedQueryTab!.EditorText = "SELECT 2";

            viewModel.CloseQueryCommand.Execute(null);

            Assert.Equal(new[] { "report.sql" }, dialogService.SaveChangesPrompts);
        }

        [Fact]
        public async Task CloseQuery_LastTab_LeavesNoSelection_AndDisablesExecute()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();

            viewModel.CloseQueryCommand.Execute(null);

            Assert.Empty(viewModel.Tabs);
            Assert.Null(viewModel.SelectedTab);
            Assert.False(viewModel.HasTabs);
            Assert.False(viewModel.ExecuteQueryCommand.CanExecute(null));
            Assert.False(viewModel.SaveFileCommand.CanExecute(null));
            Assert.False(viewModel.SaveFileAsCommand.CanExecute(null));
        }

        [Fact]
        public void CloseOtherQueries_KeepsOnlyTheGivenTab()
        {
            MainViewModel viewModel = CreateViewModel();
            viewModel.NewQueryCommand.Execute(null);
            viewModel.NewQueryCommand.Execute(null);
            QueryTabViewModel keep = QueryTab(viewModel, 1);

            viewModel.CloseOtherQueriesCommand.Execute(keep);

            Assert.Same(keep, Assert.Single(viewModel.Tabs));
            Assert.Same(keep, viewModel.SelectedTab);
        }

        [Fact]
        public void CloseOtherQueries_StopsWhenUserCancels()
        {
            MainViewModel viewModel = CreateViewModel();
            viewModel.NewQueryCommand.Execute(null);
            QueryTabViewModel keep = QueryTab(viewModel, 1);
            QueryTab(viewModel, 0).EditorText = "unsaved";
            dialogService.SaveChangesResult = SaveChangesChoice.Cancel;

            viewModel.CloseOtherQueriesCommand.Execute(keep);

            Assert.Equal(2, viewModel.Tabs.Count);
        }

        [Fact]
        public void OpenFile_ReusesPristineSelectedTab()
        {
            fileService.AddFile(@"C:\Scripts\report.sql", "SELECT * FROM Report");
            dialogService.OpenFileResult.Add(@"C:\Scripts\report.sql");
            MainViewModel viewModel = CreateViewModel();

            viewModel.OpenFileCommand.Execute(null);

            QueryTabViewModel tab = Assert.Single(viewModel.QueryTabs);
            Assert.Equal("report.sql", tab.Header);
            Assert.Equal(@"C:\Scripts\report.sql", tab.FilePath);
            Assert.Equal("SELECT * FROM Report", tab.EditorText);
            Assert.False(tab.IsModified);
        }

        [Fact]
        public void OpenFile_WhenSelectedTabHasText_OpensNewTab()
        {
            fileService.AddFile(@"C:\Scripts\report.sql", "SELECT * FROM Report");
            dialogService.OpenFileResult.Add(@"C:\Scripts\report.sql");
            MainViewModel viewModel = CreateViewModel();
            QueryTab(viewModel, 0).EditorText = "SELECT 1";

            viewModel.OpenFileCommand.Execute(null);

            Assert.Equal(2, viewModel.Tabs.Count);
            Assert.Equal("report.sql", viewModel.SelectedQueryTab!.Header);
            Assert.Equal("SELECT 1", QueryTab(viewModel, 0).EditorText);
        }

        [Fact]
        public void OpenFile_SeveralFiles_OpensOneTabEach()
        {
            fileService.AddFile(@"C:\Scripts\a.sql", "SELECT 'a'");
            fileService.AddFile(@"C:\Scripts\b.sql", "SELECT 'b'");
            dialogService.OpenFileResult.AddRange(new[] { @"C:\Scripts\a.sql", @"C:\Scripts\b.sql" });
            MainViewModel viewModel = CreateViewModel();

            viewModel.OpenFileCommand.Execute(null);

            Assert.Equal(new[] { "a.sql", "b.sql" }, viewModel.Tabs.Select(tab => tab.Header));
            Assert.Equal("b.sql", viewModel.SelectedQueryTab!.Header);
        }

        [Fact]
        public void OpenFile_AlreadyOpen_ActivatesExistingTab()
        {
            fileService.AddFile(@"C:\Scripts\report.sql", "SELECT * FROM Report");
            MainViewModel viewModel = CreateViewModel();
            viewModel.OpenFiles(new[] { @"C:\Scripts\report.sql" });
            QueryTabViewModel reportTab = viewModel.SelectedQueryTab!;
            viewModel.NewQueryCommand.Execute(null);

            // Same file, different letter case: still the same file on Windows.
            viewModel.OpenFiles(new[] { @"c:\scripts\REPORT.sql" });

            Assert.Equal(2, viewModel.Tabs.Count);
            Assert.Same(reportTab, viewModel.SelectedTab);
        }

        [Fact]
        public void OpenFile_Cancelled_ChangesNothing()
        {
            MainViewModel viewModel = CreateViewModel();

            viewModel.OpenFileCommand.Execute(null);

            QueryTabViewModel tab = Assert.Single(viewModel.QueryTabs);
            Assert.Null(tab.FilePath);
            Assert.Equal(0, dialogService.ErrorCount);
        }

        [Fact]
        public void OpenFile_UnreadableFile_ShowsError_AndOpensNoTab()
        {
            dialogService.OpenFileResult.Add(@"C:\Scripts\missing.sql");
            MainViewModel viewModel = CreateViewModel();
            QueryTab(viewModel, 0).EditorText = "SELECT 1";

            viewModel.OpenFileCommand.Execute(null);

            Assert.Equal(1, dialogService.ErrorCount);
            Assert.Single(viewModel.Tabs);
        }

        [Fact]
        public void OpenFile_DialogStartsInFolderOfLastFile()
        {
            fileService.AddFile(@"C:\Scripts\report.sql", "SELECT 1");
            MainViewModel viewModel = CreateViewModel();
            viewModel.OpenFiles(new[] { @"C:\Scripts\report.sql" });
            viewModel.NewQueryCommand.Execute(null);

            viewModel.OpenFileCommand.Execute(null);

            Assert.Equal(@"C:\Scripts", dialogService.OpenFileDirectories.Last());
        }

        [Fact]
        public void SaveFile_NewTab_AsksForFileName_AndLinksTabToFile()
        {
            MainViewModel viewModel = CreateViewModel();
            viewModel.NewQueryCommand.Execute(null);
            viewModel.SelectedQueryTab!.EditorText = "SELECT 2";
            dialogService.SaveFileResult = @"C:\Scripts\second.sql";

            viewModel.SaveFileCommand.Execute(null);

            (string suggested, string? _) = Assert.Single(dialogService.SaveFilePrompts);
            Assert.Equal("SQLQuery2.sql", suggested);
            Assert.Equal("SELECT 2", fileService.Files[@"C:\Scripts\second.sql"].Text);
            Assert.Same(fileService.DefaultEncoding, fileService.Files[@"C:\Scripts\second.sql"].Encoding);
            Assert.Equal("second.sql", viewModel.SelectedQueryTab!.Header);
            Assert.False(viewModel.SelectedQueryTab!.IsModified);
            Assert.Equal("SQLQuery1", QueryTab(viewModel, 0).Header);
        }

        [Fact]
        public void SaveFile_OpenedFile_OverwritesWithoutDialog_AndKeepsEncoding()
        {
            Encoding utf16 = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
            fileService.AddFile(@"C:\Scripts\report.sql", "SELECT 1", utf16);
            MainViewModel viewModel = CreateViewModel();
            viewModel.OpenFiles(new[] { @"C:\Scripts\report.sql" });
            viewModel.SelectedQueryTab!.EditorText = "SELECT 2";

            viewModel.SaveFileCommand.Execute(null);

            Assert.Empty(dialogService.SaveFilePrompts);
            Assert.Equal("SELECT 2", fileService.Files[@"C:\Scripts\report.sql"].Text);
            Assert.Same(utf16, fileService.Files[@"C:\Scripts\report.sql"].Encoding);
            Assert.False(viewModel.SelectedQueryTab!.IsModified);
        }

        [Fact]
        public void SaveFileAs_WritesNewFile_AndSwitchesTabToIt()
        {
            fileService.AddFile(@"C:\Scripts\report.sql", "SELECT 1");
            MainViewModel viewModel = CreateViewModel();
            viewModel.OpenFiles(new[] { @"C:\Scripts\report.sql" });
            viewModel.SelectedQueryTab!.EditorText = "SELECT 2";
            dialogService.SaveFileResult = @"C:\Archive\report-v2.sql";

            viewModel.SaveFileAsCommand.Execute(null);

            Assert.Equal(("report.sql", (string?)@"C:\Scripts"), Assert.Single(dialogService.SaveFilePrompts));
            Assert.Equal("SELECT 1", fileService.Files[@"C:\Scripts\report.sql"].Text);
            Assert.Equal("SELECT 2", fileService.Files[@"C:\Archive\report-v2.sql"].Text);
            Assert.Equal(@"C:\Archive\report-v2.sql", viewModel.SelectedQueryTab!.FilePath);
            Assert.Equal("report-v2.sql", viewModel.SelectedQueryTab!.Header);
        }

        [Fact]
        public void SaveFile_GivenTab_SavesThatTabNotTheSelectedOne()
        {
            fileService.AddFile(@"C:\Scripts\a.sql", "SELECT 'a'");
            MainViewModel viewModel = CreateViewModel();
            viewModel.OpenFiles(new[] { @"C:\Scripts\a.sql" });
            QueryTabViewModel fileTab = viewModel.SelectedQueryTab!;
            fileTab.EditorText = "SELECT 'changed'";
            viewModel.NewQueryCommand.Execute(null);

            viewModel.SaveFileCommand.Execute(fileTab);

            Assert.Equal("SELECT 'changed'", fileService.Files[@"C:\Scripts\a.sql"].Text);
            Assert.False(fileTab.IsModified);
            Assert.Empty(dialogService.SaveFilePrompts);
        }

        [Fact]
        public void SaveFile_WriteFails_ShowsError_AndKeepsTabModified()
        {
            fileService.AddFile(@"C:\Scripts\report.sql", "SELECT 1");
            MainViewModel viewModel = CreateViewModel();
            viewModel.OpenFiles(new[] { @"C:\Scripts\report.sql" });
            viewModel.SelectedQueryTab!.EditorText = "SELECT 2";
            fileService.WriteFailure = new UnauthorizedAccessException("Access denied.");

            viewModel.SaveFileCommand.Execute(null);

            Assert.Equal(1, dialogService.ErrorCount);
            Assert.True(viewModel.SelectedQueryTab!.IsModified);
            Assert.Equal("SELECT 1", fileService.Files[@"C:\Scripts\report.sql"].Text);
        }

        [Fact]
        public void OpenQueryTab_OpensNewTabWithDatabaseAndSql()
        {
            MainViewModel viewModel = CreateViewModel();
            IMainActions actions = viewModel;

            actions.OpenQueryTab("Sales", "SELECT TOP 1000 * FROM [dbo].[Orders]");

            Assert.Equal(2, viewModel.Tabs.Count);
            QueryTabViewModel tab = viewModel.SelectedQueryTab!;
            Assert.Equal("Sales", tab.ActiveDatabase);
            Assert.Equal("SELECT TOP 1000 * FROM [dbo].[Orders]", tab.EditorText);
            Assert.False(tab.IsModified);
            Assert.Contains("Sales", viewModel.Databases);
        }

        [Fact]
        public async Task Execute_RunsSelectedTab_AgainstItsOwnDatabase()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            viewModel.SelectedQueryTab!.ActiveDatabase = "Sales";
            viewModel.SelectedQueryTab!.EditorText = "SELECT 'sales'";
            viewModel.NewQueryCommand.Execute(null);
            viewModel.SelectedQueryTab!.ActiveDatabase = "HR";
            viewModel.SelectedQueryTab!.EditorText = "SELECT 'hr'";

            await viewModel.ExecuteQueryCommand.ExecuteAsync(null);

            (string Sql, string? Database) batch = Assert.Single(sqlService.ExecutedBatches);
            Assert.Equal("SELECT 'hr'", batch.Sql);
            Assert.Equal("HR", batch.Database);
            Assert.Single(viewModel.SelectedQueryTab!.Results.ResultViews);
            Assert.Empty(QueryTab(viewModel, 0).Results.ResultViews);
        }

        [Fact]
        public async Task Execute_AnotherTabCanRun_WhileOneIsStillExecuting()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            TaskCompletionSource<QueryExecutionResult> slowQuery = new();
            sqlService.ExecuteHandler = (sql, database, token) => slowQuery.Task;

            QueryTabViewModel first = viewModel.SelectedQueryTab!;
            first.EditorText = "SELECT 1";
            Task firstRun = viewModel.ExecuteQueryCommand.ExecuteAsync(null);

            Assert.True(first.IsExecuting);
            Assert.False(viewModel.ExecuteQueryCommand.CanExecute(null));
            Assert.True(viewModel.CancelCommand.CanExecute(null));

            viewModel.NewQueryCommand.Execute(null);

            // The new tab is idle, so Execute is available again while the first runs.
            Assert.True(viewModel.ExecuteQueryCommand.CanExecute(null));
            Assert.False(viewModel.CancelCommand.CanExecute(null));

            slowQuery.SetResult(FakeSqlConnectionService.CreateResult(2));
            await firstRun;

            Assert.False(first.IsExecuting);
            Assert.Equal("2 row(s)", first.RowCountText);
        }

        [Fact]
        public async Task Cancel_CancelsOnlyTheSelectedTab()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            sqlService.ExecuteHandler = async (sql, database, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return new QueryExecutionResult();
            };

            QueryTabViewModel first = viewModel.SelectedQueryTab!;
            first.EditorText = "WAITFOR DELAY '01:00:00'";
            Task firstRun = viewModel.ExecuteQueryCommand.ExecuteAsync(null);

            viewModel.NewQueryCommand.Execute(null);
            QueryTabViewModel second = viewModel.SelectedQueryTab!;
            second.EditorText = "WAITFOR DELAY '01:00:00'";
            Task secondRun = viewModel.ExecuteQueryCommand.ExecuteAsync(null);

            viewModel.CancelCommand.Execute(null);
            await secondRun;

            Assert.False(second.IsExecuting);
            Assert.True(first.IsExecuting);

            first.CancelExecution();
            await firstRun;
        }

        [Fact]
        public async Task RefreshDatabaseList_KeepsEachTabDatabase()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            viewModel.SelectedQueryTab!.ActiveDatabase = "Sales";
            viewModel.NewQueryCommand.Execute(null);
            viewModel.SelectedQueryTab!.ActiveDatabase = "HR";

            await viewModel.RefreshDatabaseListAsync();

            Assert.Equal("Sales", QueryTab(viewModel, 0).ActiveDatabase);
            Assert.Equal("HR", QueryTab(viewModel, 1).ActiveDatabase);
        }

        [Fact]
        public async Task RefreshDatabaseList_DroppedDatabase_FallsBackToDefault()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            viewModel.SelectedQueryTab!.ActiveDatabase = "HR";
            sqlService.DatabaseNames.Remove("HR");

            await viewModel.RefreshDatabaseListAsync();

            Assert.Equal("master", viewModel.SelectedQueryTab!.ActiveDatabase);
        }

        [Fact]
        public async Task SwitchingTabs_SelectedTabShowsItsOwnDatabase()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            QueryTabViewModel first = viewModel.SelectedQueryTab!;
            first.ActiveDatabase = "Sales";
            viewModel.NewQueryCommand.Execute(null);
            QueryTabViewModel second = viewModel.SelectedQueryTab!;
            second.ActiveDatabase = "HR";

            viewModel.SelectedTab = first;
            Assert.Equal("Sales", viewModel.SelectedQueryTab!.ActiveDatabase);

            viewModel.SelectedTab = second;
            Assert.Equal("HR", viewModel.SelectedQueryTab!.ActiveDatabase);
        }

        [Fact]
        public async Task TabDatabaseMissingFromList_IsAddedInSortedPosition()
        {
            sqlService.DatabaseNames.Clear();
            sqlService.DatabaseNames.AddRange(new[] { "HR", "master", "Sales" });
            MainViewModel viewModel = await CreateConnectedViewModelAsync();

            // E.g. a database created after the list was loaded, reached with USE.
            viewModel.SelectedQueryTab!.ActiveDatabase = "Inventory";

            Assert.Equal(new[] { "HR", "Inventory", "master", "Sales" }, viewModel.Databases);
            Assert.Equal("Inventory", viewModel.SelectedQueryTab!.ActiveDatabase);
        }

        [Fact]
        public async Task TabDatabaseDifferingOnlyInCase_UsesListedSpelling()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();

            viewModel.SelectedQueryTab!.ActiveDatabase = "sales";

            Assert.Equal("Sales", viewModel.SelectedQueryTab!.ActiveDatabase);
            Assert.Single(viewModel.Databases, name => string.Equals(name, "Sales", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Execute_UseInBatch_ChangesOnlyThatTabDatabase()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            QueryTabViewModel first = viewModel.SelectedQueryTab!;
            viewModel.NewQueryCommand.Execute(null);
            QueryTabViewModel second = viewModel.SelectedQueryTab!;
            second.EditorText = "USE HR;";
            sqlService.ExecuteHandler = (sql, database, token) =>
            {
                QueryExecutionResult result = FakeSqlConnectionService.CreateResult(0);
                result.NewDatabaseContext = "HR";

                return Task.FromResult(result);
            };

            await viewModel.ExecuteQueryCommand.ExecuteAsync(null);

            Assert.Equal("HR", second.ActiveDatabase);
            Assert.Equal("master", first.ActiveDatabase);

            viewModel.SelectedTab = first;
            Assert.Equal("master", viewModel.SelectedQueryTab!.ActiveDatabase);
        }

        [Fact]
        public async Task Connect_GivesOpenTabsTheDefaultDatabase()
        {
            MainViewModel viewModel = CreateViewModel();
            viewModel.NewQueryCommand.Execute(null);

            await ConnectAsync(viewModel);

            Assert.All(viewModel.QueryTabs, tab => Assert.Equal("master", tab.ActiveDatabase));
            Assert.Equal(sqlService.DatabaseNames, viewModel.Databases);
        }

        [Fact]
        public async Task Disconnect_ClearsTabDatabases_ButKeepsTabs()
        {
            MainViewModel viewModel = await CreateConnectedViewModelAsync();
            viewModel.NewQueryCommand.Execute(null);

            viewModel.DisconnectCommand.Execute(null);

            Assert.Equal(2, viewModel.Tabs.Count);
            Assert.All(viewModel.QueryTabs, tab => Assert.Null(tab.ActiveDatabase));
            Assert.Empty(viewModel.Databases);
            Assert.False(viewModel.ExecuteQueryCommand.CanExecute(null));
        }

        [Fact]
        public void ConfirmCloseAll_PromptsOnlyForModifiedTabs()
        {
            MainViewModel viewModel = CreateViewModel();
            viewModel.NewQueryCommand.Execute(null);
            viewModel.NewQueryCommand.Execute(null);
            QueryTab(viewModel, 1).EditorText = "SELECT 1";
            dialogService.SaveChangesResult = SaveChangesChoice.Discard;

            bool canClose = viewModel.ConfirmCloseAll();

            Assert.True(canClose);
            Assert.Equal(new[] { "SQLQuery2" }, dialogService.SaveChangesPrompts);
        }

        [Fact]
        public void ConfirmCloseAll_Cancel_PreventsClosing()
        {
            MainViewModel viewModel = CreateViewModel();
            QueryTab(viewModel, 0).EditorText = "SELECT 1";
            dialogService.SaveChangesResult = SaveChangesChoice.Cancel;

            Assert.False(viewModel.ConfirmCloseAll());
        }

        /// <summary>
        /// Creates a (disconnected) main view model wired to the test fakes.
        /// </summary>
        /// <returns>The view model.</returns>
        private MainViewModel CreateViewModel()
        {
            ObjectExplorerViewModel objectExplorer = new(sqlService, dialogService);

            return new MainViewModel(
                sqlService,
                new SqlStatementAnalyzer(),
                fileService,
                optionsService,
                dialogService,
                objectExplorer,
                mcpHostFactory,
                clipboardService,
                new FakeMetadataStore(),
                new LightSQL.Services.Database.DatabaseProviderRegistry(),
                new FakeLlmClientFactory());
        }

        /// <summary>
        /// Returns the tab at an index as a query tab.
        /// </summary>
        /// <param name="viewModel">The main view model.</param>
        /// <param name="index">The tab index.</param>
        /// <returns>The query tab.</returns>
        private static QueryTabViewModel QueryTab(MainViewModel viewModel, int index)
        {
            return Assert.IsType<QueryTabViewModel>(viewModel.Tabs[index]);
        }

        /// <summary>
        /// Creates a main view model and connects it to the fake server.
        /// </summary>
        /// <returns>The connected view model.</returns>
        private async Task<MainViewModel> CreateConnectedViewModelAsync()
        {
            MainViewModel viewModel = CreateViewModel();

            await ConnectAsync(viewModel);

            return viewModel;
        }

        /// <summary>
        /// Connects a view model through its Connect command using a fake profile.
        /// </summary>
        /// <param name="viewModel">The view model to connect.</param>
        /// <returns>A task that completes when connected.</returns>
        private async Task ConnectAsync(MainViewModel viewModel)
        {
            dialogService.ConnectionProfileResult = new ConnectionProfile { Server = "fake-server" };

            await viewModel.ConnectCommand.ExecuteAsync(null);
        }
    }
}
