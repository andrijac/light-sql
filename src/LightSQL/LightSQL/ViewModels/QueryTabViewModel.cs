using CommunityToolkit.Mvvm.ComponentModel;
using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// One query document (tab), like an SSMS query window. Each tab has its own SQL
    /// text, caret/selection, active database, results, execution state and
    /// cancellation, so several queries can be edited and executed independently.
    /// </summary>
    public partial class QueryTabViewModel : ObservableObject
    {
        private readonly ISqlConnectionService sqlService;
        private readonly ISqlStatementAnalyzer statementAnalyzer;
        private readonly IOptionsService optionsService;
        private readonly IDialogService dialogService;

        /// <summary>
        /// Cancels the running execution of this tab; null when idle.
        /// </summary>
        private CancellationTokenSource? executionCancellation;

        /// <summary>
        /// True while text is loaded programmatically, so the load does not mark the
        /// tab as modified.
        /// </summary>
        private bool isLoadingText;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPristine))]
        private string editorText = string.Empty;

        [ObservableProperty]
        private string selectedText = string.Empty;

        [ObservableProperty]
        private int caretLine = 1;

        [ObservableProperty]
        private int caretColumn = 1;

        [ObservableProperty]
        private string? activeDatabase;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        [NotifyPropertyChangedFor(nameof(Header))]
        [NotifyPropertyChangedFor(nameof(IsPristine))]
        private string? queryName;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Header))]
        [NotifyPropertyChangedFor(nameof(IsPristine))]
        private bool isModified;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool isExecuting;

        [ObservableProperty]
        private string executionStatus = "Ready";

        [ObservableProperty]
        private string rowCountText = string.Empty;

        [ObservableProperty]
        private string elapsedText = string.Empty;

        /// <summary>
        /// Suffix appended to the header of a tab with unsaved changes.
        /// </summary>
        private const string ModifiedMarker = " *";

        /// <summary>
        /// Creates a query tab.
        /// </summary>
        /// <param name="sqlService">SQL Server connection/execution service.</param>
        /// <param name="statementAnalyzer">Analyzer for the UPDATE/DELETE guard.</param>
        /// <param name="optionsService">Application options.</param>
        /// <param name="dialogService">User prompts and dialogs.</param>
        /// <param name="defaultTitle">Title used until the query is saved, e.g. "SQLQuery1".</param>
        public QueryTabViewModel(
            ISqlConnectionService sqlService,
            ISqlStatementAnalyzer statementAnalyzer,
            IOptionsService optionsService,
            IDialogService dialogService,
            string defaultTitle)
        {
            this.sqlService = sqlService;
            this.statementAnalyzer = statementAnalyzer;
            this.optionsService = optionsService;
            this.dialogService = dialogService;

            DefaultTitle = defaultTitle;
        }

        /// <summary>
        /// The title used while the query has not been saved (e.g. "SQLQuery1").
        /// </summary>
        public string DefaultTitle { get; }

        /// <summary>
        /// The results and messages of the last execution in this tab.
        /// </summary>
        public ResultsViewModel Results { get; } = new();

        /// <summary>
        /// The saved query name, or the default title when not saved yet.
        /// </summary>
        public string DisplayName
        {
            get
            {
                return QueryName ?? DefaultTitle;
            }
        }

        /// <summary>
        /// The tab header text: the display name, with " *" when there are unsaved changes.
        /// </summary>
        public string Header
        {
            get
            {
                return IsModified == true ? DisplayName + ModifiedMarker : DisplayName;
            }
        }

        /// <summary>
        /// True for an untouched new query (no name, no text, no changes). Such a tab
        /// can be reused when opening a saved query instead of adding another tab.
        /// </summary>
        public bool IsPristine
        {
            get
            {
                return QueryName is null && IsModified == false && string.IsNullOrEmpty(EditorText) == true;
            }
        }

        /// <summary>
        /// Replaces the editor text without marking the tab as modified (used when
        /// opening a saved query or generating a script).
        /// </summary>
        /// <param name="sql">The SQL to show.</param>
        /// <param name="name">The saved query name, or null for an unsaved query.</param>
        public void LoadText(string sql, string? name)
        {
            isLoadingText = true;

            try
            {
                EditorText = sql;
            }
            finally
            {
                isLoadingText = false;
            }

            QueryName = name;
            IsModified = false;
        }

        /// <summary>
        /// Records that the tab's text was saved under the given name.
        /// </summary>
        /// <param name="name">The name the query was saved as.</param>
        public void MarkSaved(string name)
        {
            QueryName = name;
            IsModified = false;
        }

        /// <summary>
        /// Requests cancellation of the running execution, if any.
        /// </summary>
        public void CancelExecution()
        {
            executionCancellation?.Cancel();
        }

        /// <summary>
        /// Executes this tab's query (the selection if any, otherwise the whole text)
        /// against the tab's active database, applying the UPDATE/DELETE-without-WHERE
        /// guard first.
        /// </summary>
        /// <returns>A task that completes when execution finishes.</returns>
        public async Task ExecuteAsync()
        {
            if (IsExecuting == true)
            {
                return;
            }

            if (sqlService.IsConnected == false)
            {
                dialogService.ShowInformation("Connect to a server first.", "Not Connected");

                return;
            }

            string sql = string.IsNullOrWhiteSpace(SelectedText) == false ? SelectedText : EditorText;

            if (string.IsNullOrWhiteSpace(sql) == true)
            {
                return;
            }

            bool needsGuard = optionsService.Current.WarnOnUpdateDeleteWithoutWhere == true
                && statementAnalyzer.HasUnguardedUpdateOrDelete(sql) == true;

            if (needsGuard == true)
            {
                bool proceed = dialogService.Confirm(
                    "This batch contains an UPDATE or DELETE with no WHERE clause, which affects every row. Execute anyway?",
                    "Confirm Execution");

                if (proceed == false)
                {
                    ExecutionStatus = "Execution cancelled";

                    return;
                }
            }

            IsExecuting = true;
            ExecutionStatus = "Executing...";
            RowCountText = string.Empty;
            ElapsedText = string.Empty;
            executionCancellation = new CancellationTokenSource();

            try
            {
                QueryExecutionResult result = await sqlService.ExecuteBatchAsync(sql, ActiveDatabase, executionCancellation.Token);

                Results.Load(result);
                ElapsedText = $"{result.Elapsed.TotalMilliseconds:N0} ms";

                if (result.TotalRows > 0)
                {
                    RowCountText = $"{result.TotalRows} row(s)";
                }
                else
                {
                    RowCountText = $"{result.RecordsAffected} row(s) affected";
                }

                ExecutionStatus = result.IsSuccess == true ? "Query executed successfully" : "Query completed with errors";
            }
            catch (OperationCanceledException)
            {
                Results.ShowMessage("Query cancelled.");
                ExecutionStatus = "Query cancelled";
            }
            catch (Exception ex)
            {
                Results.ShowMessage(ex.Message);
                ExecutionStatus = "Execution error";
            }
            finally
            {
                executionCancellation?.Dispose();
                executionCancellation = null;
                IsExecuting = false;
            }
        }

        /// <summary>
        /// Marks the tab as modified when the user edits the text.
        /// </summary>
        /// <param name="value">The new editor text.</param>
        partial void OnEditorTextChanged(string value)
        {
            if (isLoadingText == false)
            {
                IsModified = true;
            }
        }
    }
}
