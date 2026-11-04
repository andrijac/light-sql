using System.Text;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.ViewModels;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests a single query tab: title/modified state and per-tab execution.
    /// </summary>
    public class QueryTabViewModelTests
    {
        private readonly FakeSqlConnectionService sqlService = new() { IsConnected = true, CurrentDatabase = "master" };
        private readonly FakeDialogService dialogService = new();
        private readonly FakeOptionsService optionsService = new();

        [Fact]
        public void NewTab_UsesDefaultTitle_AndIsPristine()
        {
            QueryTabViewModel tab = CreateTab();

            Assert.Equal("SQLQuery1", tab.Header);
            Assert.Equal("SQLQuery1", tab.DisplayName);
            Assert.False(tab.IsModified);
            Assert.True(tab.IsPristine);
        }

        [Fact]
        public void EditingText_MarksModified_AndAddsMarkerToHeader()
        {
            QueryTabViewModel tab = CreateTab();

            tab.EditorText = "SELECT 1";

            Assert.True(tab.IsModified);
            Assert.False(tab.IsPristine);
            Assert.Equal("SQLQuery1 *", tab.Header);
        }

        [Fact]
        public void LoadText_DoesNotMarkModified_AndHasNoFile()
        {
            QueryTabViewModel tab = CreateTab();

            tab.LoadText("SELECT 1");

            Assert.Equal("SELECT 1", tab.EditorText);
            Assert.False(tab.IsModified);
            Assert.Null(tab.FilePath);
            Assert.Equal("SQLQuery1", tab.Header);
        }

        [Fact]
        public void LoadFile_ShowsFileName_KeepsEncoding_AndIsNotModified()
        {
            QueryTabViewModel tab = CreateTab();
            Encoding encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);

            tab.LoadFile(@"C:\Scripts\daily-report.sql", new TextFileContent("SELECT 1", encoding));

            Assert.Equal("SELECT 1", tab.EditorText);
            Assert.False(tab.IsModified);
            Assert.False(tab.IsPristine);
            Assert.Equal(@"C:\Scripts\daily-report.sql", tab.FilePath);
            Assert.Equal("daily-report.sql", tab.Header);
            Assert.Equal(@"C:\Scripts\daily-report.sql", tab.ToolTip);
            Assert.Same(encoding, tab.FileEncoding);
        }

        [Fact]
        public void SuggestedFileName_UsesDefaultTitle_UntilSaved()
        {
            QueryTabViewModel tab = CreateTab();

            Assert.Equal("SQLQuery1.sql", tab.SuggestedFileName);

            tab.MarkSaved(@"C:\Scripts\orders.sql", Encoding.UTF8);

            Assert.Equal("orders.sql", tab.SuggestedFileName);
        }

        [Fact]
        public void MarkSaved_ClearsModified_AndShowsFileName()
        {
            QueryTabViewModel tab = CreateTab();
            tab.EditorText = "SELECT 1";

            tab.MarkSaved(@"C:\Scripts\saved.sql", Encoding.UTF8);

            Assert.False(tab.IsModified);
            Assert.Equal("saved.sql", tab.Header);
            Assert.Equal(@"C:\Scripts\saved.sql", tab.FilePath);
        }

        [Fact]
        public void EditingFile_AddsMarkerToFileName()
        {
            QueryTabViewModel tab = CreateTab();
            tab.LoadFile(@"C:\Scripts\report.sql", new TextFileContent("SELECT 1", Encoding.UTF8));

            tab.EditorText = "SELECT 2";

            Assert.Equal("report.sql *", tab.Header);
        }

        [Fact]
        public async Task Execute_RunsAgainstTheTabDatabase_AndLoadsResults()
        {
            QueryTabViewModel tab = CreateTab();
            tab.ActiveDatabase = "Sales";
            tab.EditorText = "SELECT * FROM Orders";
            sqlService.ExecuteHandler = (sql, database, token) => Task.FromResult(FakeSqlConnectionService.CreateResult(3));

            await tab.ExecuteAsync();

            (string Sql, string? Database) batch = Assert.Single(sqlService.ExecutedBatches);
            Assert.Equal("SELECT * FROM Orders", batch.Sql);
            Assert.Equal("Sales", batch.Database);
            Assert.Single(tab.Results.ResultViews);
            Assert.Equal("3 row(s)", tab.RowCountText);
            Assert.Equal("Query executed successfully", tab.ExecutionStatus);
            Assert.False(tab.IsExecuting);
        }

        [Fact]
        public async Task Execute_PrefersSelectedText()
        {
            QueryTabViewModel tab = CreateTab();
            tab.EditorText = "SELECT 1\nSELECT 2";
            tab.SelectedText = "SELECT 2";

            await tab.ExecuteAsync();

            Assert.Equal("SELECT 2", Assert.Single(sqlService.ExecutedBatches).Sql);
        }

        [Fact]
        public async Task Execute_WithoutRows_ReportsRowsAffected()
        {
            QueryTabViewModel tab = CreateTab();
            tab.EditorText = "UPDATE t SET a = 1 WHERE id = 5";
            sqlService.ExecuteHandler = (sql, database, token) => Task.FromResult(new QueryExecutionResult
            {
                IsSuccess = true,
                RecordsAffected = 4,
                Messages = "(4 row(s) affected)"
            });

            await tab.ExecuteAsync();

            Assert.Equal("4 row(s) affected", tab.RowCountText);
            Assert.Empty(tab.Results.ResultViews);
        }

        [Fact]
        public async Task Execute_UnguardedDelete_Declined_DoesNotRun()
        {
            QueryTabViewModel tab = CreateTab();
            tab.EditorText = "DELETE FROM Orders";
            dialogService.ConfirmResult = false;

            await tab.ExecuteAsync();

            Assert.Equal(1, dialogService.ConfirmCount);
            Assert.Empty(sqlService.ExecutedBatches);
            Assert.Equal("Execution cancelled", tab.ExecutionStatus);
        }

        [Fact]
        public async Task Execute_UnguardedDelete_WithGuardDisabled_RunsWithoutAsking()
        {
            optionsService.Save(new AppOptions { WarnOnUpdateDeleteWithoutWhere = false });
            QueryTabViewModel tab = CreateTab();
            tab.EditorText = "DELETE FROM Orders";

            await tab.ExecuteAsync();

            Assert.Equal(0, dialogService.ConfirmCount);
            Assert.Single(sqlService.ExecutedBatches);
        }

        [Fact]
        public async Task Execute_WhenNotConnected_InformsAndDoesNotRun()
        {
            sqlService.IsConnected = false;
            QueryTabViewModel tab = CreateTab();
            tab.EditorText = "SELECT 1";

            await tab.ExecuteAsync();

            Assert.Equal(1, dialogService.InformationCount);
            Assert.Empty(sqlService.ExecutedBatches);
        }

        [Fact]
        public async Task CancelExecution_StopsTheRunningQuery()
        {
            QueryTabViewModel tab = CreateTab();
            tab.EditorText = "WAITFOR DELAY '01:00:00'";
            sqlService.ExecuteHandler = async (sql, database, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return new QueryExecutionResult();
            };

            Task running = tab.ExecuteAsync();
            Assert.True(tab.IsExecuting);

            tab.CancelExecution();
            await running;

            Assert.False(tab.IsExecuting);
            Assert.Equal("Query cancelled", tab.ExecutionStatus);
            Assert.Equal("Query cancelled.", tab.Results.Messages);
        }

        [Fact]
        public async Task Execute_ServiceFailure_ShowsErrorInMessages()
        {
            QueryTabViewModel tab = CreateTab();
            tab.EditorText = "SELECT 1";
            sqlService.ExecuteHandler = (sql, database, token) => throw new InvalidOperationException("boom");

            await tab.ExecuteAsync();

            Assert.Equal("Execution error", tab.ExecutionStatus);
            Assert.Equal("boom", tab.Results.Messages);
            Assert.False(tab.IsExecuting);
        }

        /// <summary>
        /// Creates a tab wired to the test fakes.
        /// </summary>
        /// <returns>The new tab.</returns>
        private QueryTabViewModel CreateTab()
        {
            return new QueryTabViewModel(sqlService, new SqlStatementAnalyzer(), optionsService, dialogService, "SQLQuery1");
        }
    }
}
