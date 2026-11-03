using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// The main window's view model. Orchestrates connection, query execution,
    /// saved queries, options and the Object Explorer.
    /// </summary>
    public partial class MainViewModel : ObservableObject, IMainActions
    {
        private readonly ISqlConnectionService sqlService;
        private readonly ISqlStatementAnalyzer statementAnalyzer;
        private readonly IQueryStore queryStore;
        private readonly IOptionsService optionsService;
        private readonly IDialogService dialogService;

        private CancellationTokenSource? executionCancellation;
        private bool suppressDatabaseChange;

        [ObservableProperty]
        private string editorText = string.Empty;

        [ObservableProperty]
        private string selectedText = string.Empty;

        [ObservableProperty]
        private int caretLine = 1;

        [ObservableProperty]
        private int caretColumn = 1;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExecuteQueryCommand))]
        [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
        private bool isConnected;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExecuteQueryCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
        private bool isExecuting;

        [ObservableProperty]
        private string connectionStatus = "Not connected";

        [ObservableProperty]
        private string? activeDatabase;

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private string rowCountText = string.Empty;

        [ObservableProperty]
        private string elapsedText = string.Empty;

        [ObservableProperty]
        private string? currentQueryName;

        /// <summary>
        /// Creates the main view model.
        /// </summary>
        /// <param name="sqlService">SQL Server connection/execution service.</param>
        /// <param name="statementAnalyzer">Analyzer for the UPDATE/DELETE guard.</param>
        /// <param name="queryStore">Store for saved queries.</param>
        /// <param name="optionsService">Application options.</param>
        /// <param name="dialogService">User prompts and dialogs.</param>
        /// <param name="results">The results panel view model.</param>
        /// <param name="objectExplorer">The Object Explorer view model.</param>
        public MainViewModel(
            ISqlConnectionService sqlService,
            ISqlStatementAnalyzer statementAnalyzer,
            IQueryStore queryStore,
            IOptionsService optionsService,
            IDialogService dialogService,
            ResultsViewModel results,
            ObjectExplorerViewModel objectExplorer)
        {
            this.sqlService = sqlService;
            this.statementAnalyzer = statementAnalyzer;
            this.queryStore = queryStore;
            this.optionsService = optionsService;
            this.dialogService = dialogService;

            Results = results;
            ObjectExplorer = objectExplorer;
            ObjectExplorer.MainActions = this;

            RefreshSavedQueries();
        }

        /// <summary>
        /// Raised after options are saved so the view can re-apply editor settings.
        /// </summary>
        public event Action? OptionsApplied;

        /// <summary>
        /// The results panel.
        /// </summary>
        public ResultsViewModel Results { get; }

        /// <summary>
        /// The Object Explorer tree.
        /// </summary>
        public ObjectExplorerViewModel ObjectExplorer { get; }

        /// <summary>
        /// The databases available on the connected server (toolbar dropdown).
        /// </summary>
        public ObservableCollection<string> Databases { get; } = new();

        /// <summary>
        /// The saved queries available to open.
        /// </summary>
        public ObservableCollection<SavedQuery> SavedQueries { get; } = new();

        /// <summary>
        /// The current application options.
        /// </summary>
        public AppOptions Options
        {
            get
            {
                return optionsService.Current;
            }
        }

        /// <inheritdoc />
        public void SetEditorText(string sql)
        {
            EditorText = sql;
        }

        /// <inheritdoc />
        public void SetActiveDatabase(string database)
        {
            if (string.IsNullOrWhiteSpace(database) == true)
            {
                return;
            }

            if (Databases.Contains(database) == false)
            {
                Databases.Add(database);
            }

            ActiveDatabase = database;
        }

        /// <inheritdoc />
        public Task ExecuteAsync()
        {
            return ExecuteQueryAsync();
        }

        /// <inheritdoc />
        public async Task RefreshDatabaseListAsync()
        {
            if (sqlService.IsConnected == false)
            {
                return;
            }

            try
            {
                IReadOnlyList<string> databases = await sqlService.GetDatabasesAsync(CancellationToken.None);

                Databases.Clear();

                foreach (string database in databases)
                {
                    Databases.Add(database);
                }

                // Select the server's current database without re-issuing SetCurrentDatabase.
                suppressDatabaseChange = true;

                string? current = sqlService.CurrentDatabase;
                ActiveDatabase = current is not null && Databases.Contains(current) == true
                    ? current
                    : Databases.FirstOrDefault();

                suppressDatabaseChange = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not list databases: {ex.Message}";
            }
        }

        /// <summary>
        /// Opens the connection dialog and connects to the chosen server.
        /// </summary>
        /// <returns>A task that completes when the attempt finishes.</returns>
        [RelayCommand]
        private async Task ConnectAsync()
        {
            ConnectionProfile? profile = dialogService.ShowConnectionDialog();

            if (profile is null)
            {
                return;
            }

            StatusMessage = "Connecting...";

            try
            {
                string info = await sqlService.ConnectAsync(profile, CancellationToken.None);

                IsConnected = true;
                ConnectionStatus = info;

                await RefreshDatabaseListAsync();
                ObjectExplorer.Build(sqlService.ServerName ?? profile.Server);

                StatusMessage = "Connected";
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionStatus = "Not connected";
                StatusMessage = "Connection failed";
                dialogService.ShowError(ex.Message, "Connection Failed");
            }
        }

        /// <summary>
        /// Disconnects from the current server.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDisconnect))]
        private void Disconnect()
        {
            sqlService.Disconnect();
            IsConnected = false;
            ConnectionStatus = "Not connected";
            Databases.Clear();
            ActiveDatabase = null;
            ObjectExplorer.Clear();
            StatusMessage = "Disconnected";
        }

        /// <summary>
        /// Executes the current query (selection if any, otherwise the whole editor),
        /// applying the UPDATE/DELETE-without-WHERE guard first.
        /// </summary>
        /// <returns>A task that completes when execution finishes.</returns>
        [RelayCommand(CanExecute = nameof(CanExecuteQuery))]
        private async Task ExecuteQueryAsync()
        {
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

            if (Options.WarnOnUpdateDeleteWithoutWhere == true && statementAnalyzer.HasUnguardedUpdateOrDelete(sql) == true)
            {
                bool proceed = dialogService.Confirm(
                    "This batch contains an UPDATE or DELETE with no WHERE clause, which affects every row. Execute anyway?",
                    "Confirm Execution");

                if (proceed == false)
                {
                    StatusMessage = "Execution cancelled";

                    return;
                }
            }

            IsExecuting = true;
            StatusMessage = "Executing...";
            RowCountText = string.Empty;
            ElapsedText = string.Empty;
            executionCancellation = new CancellationTokenSource();

            try
            {
                QueryExecutionResult result = await sqlService.ExecuteBatchAsync(sql, executionCancellation.Token);

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

                StatusMessage = result.IsSuccess == true ? "Query executed successfully" : "Query completed with errors";
            }
            catch (OperationCanceledException)
            {
                Results.ShowMessage("Query cancelled.");
                StatusMessage = "Query cancelled";
            }
            catch (Exception ex)
            {
                Results.ShowMessage(ex.Message);
                StatusMessage = "Execution error";
            }
            finally
            {
                IsExecuting = false;
                executionCancellation?.Dispose();
                executionCancellation = null;
            }
        }

        /// <summary>
        /// Cancels the running query.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void Cancel()
        {
            executionCancellation?.Cancel();
        }

        /// <summary>
        /// Clears the editor to start a new query.
        /// </summary>
        [RelayCommand]
        private void NewQuery()
        {
            EditorText = string.Empty;
            CurrentQueryName = null;
            StatusMessage = "New query";
        }

        /// <summary>
        /// Saves the current editor content under a name in the personal folder.
        /// </summary>
        [RelayCommand]
        private void SaveQuery()
        {
            string? name = dialogService.PromptForText(
                "Save Query",
                "Query name:",
                CurrentQueryName ?? string.Empty,
                candidate => queryStore.IsValidName(candidate));

            if (name is null)
            {
                return;
            }

            try
            {
                queryStore.Save(name, EditorText);
                CurrentQueryName = name;
                RefreshSavedQueries();
                StatusMessage = $"Saved '{name}'";
            }
            catch (Exception ex)
            {
                dialogService.ShowError(ex.Message, "Save Query");
            }
        }

        /// <summary>
        /// Opens a saved query into the editor.
        /// </summary>
        /// <param name="query">The saved query to open.</param>
        [RelayCommand]
        private void OpenQuery(SavedQuery? query)
        {
            if (query is null)
            {
                return;
            }

            try
            {
                EditorText = queryStore.ReadContent(query.Name);
                CurrentQueryName = query.Name;
                StatusMessage = $"Opened '{query.Name}'";
            }
            catch (Exception ex)
            {
                dialogService.ShowError(ex.Message, "Open Query");
            }
        }

        /// <summary>
        /// Reloads the list of saved queries from the personal folder.
        /// </summary>
        [RelayCommand]
        private void RefreshSavedQueries()
        {
            SavedQueries.Clear();

            foreach (SavedQuery query in queryStore.GetAll())
            {
                SavedQueries.Add(query);
            }
        }

        /// <summary>
        /// Shows the Options dialog and re-applies editor settings when saved.
        /// </summary>
        [RelayCommand]
        private void ShowOptions()
        {
            bool saved = dialogService.ShowOptionsDialog();

            if (saved == true)
            {
                OptionsApplied?.Invoke();
                StatusMessage = "Options saved";
            }
        }

        /// <summary>
        /// Updates the active database on the service when the selection changes.
        /// </summary>
        /// <param name="value">The newly selected database.</param>
        partial void OnActiveDatabaseChanged(string? value)
        {
            if (suppressDatabaseChange == true)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value) == false)
            {
                sqlService.SetCurrentDatabase(value);
            }
        }

        /// <summary>
        /// Whether a query can be executed now.
        /// </summary>
        /// <returns>True when connected and not already executing.</returns>
        private bool CanExecuteQuery()
        {
            return IsConnected == true && IsExecuting == false;
        }

        /// <summary>
        /// Whether a running query can be cancelled now.
        /// </summary>
        /// <returns>True while executing.</returns>
        private bool CanCancel()
        {
            return IsExecuting == true;
        }

        /// <summary>
        /// Whether a disconnect is possible now.
        /// </summary>
        /// <returns>True when connected.</returns>
        private bool CanDisconnect()
        {
            return IsConnected == true;
        }
    }
}
