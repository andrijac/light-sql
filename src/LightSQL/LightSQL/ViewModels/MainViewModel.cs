using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Mcp;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// The main window's view model. Orchestrates the connection, the document tabs
    /// (SSMS-style query documents backed by .sql files: New, Open, Save, Save As;
    /// and MCP server tabs), options and the Object Explorer.
    /// </summary>
    public partial class MainViewModel : ObservableObject, IMainActions
    {
        private readonly ISqlConnectionService sqlService;
        private readonly ISqlStatementAnalyzer statementAnalyzer;
        private readonly IFileService fileService;
        private readonly IOptionsService optionsService;
        private readonly IDialogService dialogService;
        private readonly IMcpServerHostFactory mcpHostFactory;
        private readonly IClipboardService clipboardService;

        /// <summary>
        /// The connection used by the last successful Connect; MCP servers are
        /// started with it. Null when disconnected.
        /// </summary>
        private ConnectionProfile? connectedProfile;

        /// <summary>
        /// The folder of the last file opened or saved, offered first by the next
        /// Open / Save As dialog for a tab that has no file of its own.
        /// </summary>
        private string? lastFileDirectory;

        /// <summary>
        /// Number used for the next default tab title ("SQLQuery{n}"). Never reused,
        /// like SSMS, so titles stay unique for the session.
        /// </summary>
        private int nextQueryNumber = 1;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExecuteQueryCommand))]
        [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
        [NotifyCanExecuteChangedFor(nameof(StartMcpServerCommand))]
        private bool isConnected;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExecuteQueryCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveFileAsCommand))]
        [NotifyPropertyChangedFor(nameof(SelectedQueryTab))]
        [NotifyPropertyChangedFor(nameof(IsQueryTabSelected))]
        private DocumentTabViewModel? selectedTab;

        [ObservableProperty]
        private string connectionStatus = "Not connected";

        [ObservableProperty]
        private string statusMessage = "Ready";

        /// <summary>
        /// Prefix of the default title given to new, unsaved query tabs.
        /// </summary>
        private const string DefaultTitlePrefix = "SQLQuery";

        /// <summary>
        /// Creates the main view model and opens the first, empty query tab.
        /// </summary>
        /// <param name="sqlService">SQL Server connection/execution service.</param>
        /// <param name="statementAnalyzer">Analyzer for the UPDATE/DELETE guard.</param>
        /// <param name="fileService">Reads and writes .sql files.</param>
        /// <param name="optionsService">Application options.</param>
        /// <param name="dialogService">User prompts and dialogs.</param>
        /// <param name="objectExplorer">The Object Explorer view model.</param>
        /// <param name="mcpHostFactory">Starts MCP servers for the connection.</param>
        /// <param name="clipboardService">Clipboard access for the MCP tabs.</param>
        public MainViewModel(
            ISqlConnectionService sqlService,
            ISqlStatementAnalyzer statementAnalyzer,
            IFileService fileService,
            IOptionsService optionsService,
            IDialogService dialogService,
            ObjectExplorerViewModel objectExplorer,
            IMcpServerHostFactory mcpHostFactory,
            IClipboardService clipboardService)
        {
            this.sqlService = sqlService;
            this.statementAnalyzer = statementAnalyzer;
            this.fileService = fileService;
            this.optionsService = optionsService;
            this.dialogService = dialogService;
            this.mcpHostFactory = mcpHostFactory;
            this.clipboardService = clipboardService;

            ObjectExplorer = objectExplorer;
            ObjectExplorer.MainActions = this;

            // Start with one empty query, like SSMS's "New Query".
            AddQueryTab(null, string.Empty);
        }

        /// <summary>
        /// The Object Explorer tree.
        /// </summary>
        public ObjectExplorerViewModel ObjectExplorer { get; }

        /// <summary>
        /// The open document tabs (query tabs and MCP server tabs), in display order.
        /// </summary>
        public ObservableCollection<DocumentTabViewModel> Tabs { get; } = new();

        /// <summary>
        /// The open query tabs, in display order.
        /// </summary>
        public IEnumerable<QueryTabViewModel> QueryTabs
        {
            get
            {
                return Tabs.OfType<QueryTabViewModel>();
            }
        }

        /// <summary>
        /// The open MCP server tabs, in display order.
        /// </summary>
        public IEnumerable<McpServerTabViewModel> McpServerTabs
        {
            get
            {
                return Tabs.OfType<McpServerTabViewModel>();
            }
        }

        /// <summary>
        /// The selected tab when it is a query tab; null when no tab or another kind
        /// of tab (e.g. an MCP server) is selected. The toolbar database dropdown,
        /// Execute/Cancel/Save and the status bar act on this tab.
        /// </summary>
        public QueryTabViewModel? SelectedQueryTab
        {
            get
            {
                return SelectedTab as QueryTabViewModel;
            }
        }

        /// <summary>
        /// Whether the selected tab is a query tab (shows its execution details in the status bar).
        /// </summary>
        public bool IsQueryTabSelected
        {
            get
            {
                return SelectedQueryTab is not null;
            }
        }

        /// <summary>
        /// The databases available on the connected server (toolbar dropdown).
        /// </summary>
        public ObservableCollection<string> Databases { get; } = new();

        /// <summary>
        /// Whether at least one query tab is open.
        /// </summary>
        public bool HasTabs
        {
            get
            {
                return Tabs.Count > 0;
            }
        }

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

        /// <summary>
        /// Whether the editors show line numbers (bound by every tab's editor).
        /// </summary>
        public bool ShowLineNumbers
        {
            get
            {
                return Options.ShowLineNumbers;
            }
        }

        /// <summary>
        /// The editor font size in points (bound by every tab's editor).
        /// </summary>
        public double EditorFontSize
        {
            get
            {
                return Options.EditorFontSize;
            }
        }

        /// <inheritdoc />
        public void OpenQueryTab(string? database, string sql)
        {
            AddQueryTab(EnsureDatabaseListed(database), sql);
        }

        /// <inheritdoc />
        public async Task OpenMcpServerTabAsync(string? database)
        {
            if (connectedProfile is null || IsConnected == false)
            {
                dialogService.ShowInformation("Connect to a server first.", "Not Connected");

                return;
            }

            McpServerTabViewModel tab = new(
                mcpHostFactory,
                clipboardService,
                dialogService,
                connectedProfile,
                sqlService.ServerName ?? connectedProfile.Server,
                database);

            AddDocumentTab(tab);
            StatusMessage = "Starting MCP server...";

            await tab.StartAsync();

            StatusMessage = tab.State == McpServerState.Running
                ? $"MCP server listening on {tab.Endpoint}"
                : "MCP server could not start";
        }

        /// <summary>
        /// Stops every running MCP server (when the application closes).
        /// </summary>
        /// <returns>A task that completes when all servers have stopped.</returns>
        public Task StopAllMcpServersAsync()
        {
            return Task.WhenAll(McpServerTabs.Select(tab => tab.StopAsync()).ToList());
        }

        /// <summary>
        /// Opens files in query tabs (from the Open dialog, files dropped on the
        /// window, or files passed on the command line). A file that
        /// is already open has its tab activated; the first file reuses the selected
        /// tab when it is an untouched new query; other files get new tabs. A file that
        /// cannot be read is reported and skipped.
        /// </summary>
        /// <param name="paths">The full paths of the files to open.</param>
        public void OpenFiles(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                OpenSingleFile(path);
            }
        }

        /// <inheritdoc />
        public Task ExecuteAsync()
        {
            if (SelectedQueryTab is null)
            {
                return Task.CompletedTask;
            }

            return SelectedQueryTab.ExecuteAsync();
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

                // Remember each tab's database: clearing the list makes the toolbar
                // dropdown push a null selection into the selected tab.
                Dictionary<QueryTabViewModel, string?> previous = QueryTabs.ToDictionary(tab => tab, tab => tab.ActiveDatabase);

                Databases.Clear();

                foreach (string database in databases)
                {
                    Databases.Add(database);
                }

                string? current = sqlService.CurrentDatabase;
                string? fallback = current is not null && Databases.Contains(current) == true
                    ? current
                    : Databases.FirstOrDefault();

                foreach (QueryTabViewModel tab in QueryTabs)
                {
                    string? remembered = previous[tab];
                    tab.ActiveDatabase = remembered is not null && Databases.Contains(remembered) == true ? remembered : fallback;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not list databases: {ex.Message}";
            }
        }

        /// <summary>
        /// Asks to save every tab with unsaved changes, e.g. before the application
        /// closes.
        /// </summary>
        /// <returns>True when it is OK to close; false when the user cancelled.</returns>
        public bool ConfirmCloseAll()
        {
            foreach (QueryTabViewModel tab in QueryTabs.ToList())
            {
                if (tab.IsModified == false)
                {
                    continue;
                }

                // Show the tab being asked about.
                SelectedTab = tab;

                if (ConfirmSaveIfModified(tab) == false)
                {
                    return false;
                }
            }

            return true;
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
                connectedProfile = profile;

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
        /// Disconnects from the current server. Open query tabs stay open but lose
        /// their database. Running MCP servers keep running: each has its own
        /// connection and is stopped from its tab.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDisconnect))]
        private void Disconnect()
        {
            sqlService.Disconnect();
            IsConnected = false;
            ConnectionStatus = "Not connected";
            connectedProfile = null;
            Databases.Clear();

            foreach (QueryTabViewModel tab in QueryTabs)
            {
                tab.ActiveDatabase = null;
            }

            ObjectExplorer.Clear();
            StatusMessage = "Disconnected";
        }

        /// <summary>
        /// Executes the query in the selected tab. Concurrent executions are allowed so
        /// that another tab can run while one is still executing.
        /// </summary>
        /// <returns>A task that completes when execution finishes.</returns>
        [RelayCommand(CanExecute = nameof(CanExecuteQuery), AllowConcurrentExecutions = true)]
        private Task ExecuteQueryAsync()
        {
            return ExecuteAsync();
        }

        /// <summary>
        /// Cancels the query running in the selected tab.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void Cancel()
        {
            SelectedQueryTab?.CancelExecution();
        }

        /// <summary>
        /// Starts an MCP server for the current connection in a new tab, using the
        /// selected query tab's database as the server's default database.
        /// </summary>
        /// <returns>A task that completes when the server is running or has failed.</returns>
        [RelayCommand(CanExecute = nameof(CanDisconnect))]
        private Task StartMcpServerAsync()
        {
            string? database = SelectedQueryTab?.ActiveDatabase ?? sqlService.CurrentDatabase;

            return OpenMcpServerTabAsync(database);
        }

        /// <summary>
        /// Opens a new, empty query tab using the selected tab's database.
        /// </summary>
        [RelayCommand]
        private void NewQuery()
        {
            string? database = SelectedQueryTab?.ActiveDatabase;

            if (database is null && IsConnected == true)
            {
                database = sqlService.CurrentDatabase;
            }

            AddQueryTab(database, string.Empty);
            StatusMessage = "New query";
        }

        /// <summary>
        /// Shows the Open dialog and opens the chosen .sql files in tabs (File → Open).
        /// </summary>
        [RelayCommand]
        private void OpenFile()
        {
            IReadOnlyList<string> paths = dialogService.ShowOpenFileDialog(GetDialogDirectory(SelectedQueryTab));

            OpenFiles(paths);
        }

        /// <summary>
        /// Saves a tab to its file, or asks for a file name when it has none yet
        /// (File → Save).
        /// </summary>
        /// <param name="tab">The tab to save, or null for the selected tab.</param>
        [RelayCommand(CanExecute = nameof(HasSelectedTab))]
        private void SaveFile(QueryTabViewModel? tab)
        {
            QueryTabViewModel? target = tab ?? SelectedQueryTab;

            if (target is not null)
            {
                SaveTab(target);
            }
        }

        /// <summary>
        /// Saves a tab to a new file chosen in the Save As dialog (File → Save As).
        /// The tab then refers to the new file.
        /// </summary>
        /// <param name="tab">The tab to save, or null for the selected tab.</param>
        [RelayCommand(CanExecute = nameof(HasSelectedTab))]
        private void SaveFileAs(QueryTabViewModel? tab)
        {
            QueryTabViewModel? target = tab ?? SelectedQueryTab;

            if (target is not null)
            {
                SaveTabAs(target);
            }
        }

        /// <summary>
        /// Closes a tab (the selected one when none is given). A query tab asks to
        /// save unsaved changes first and cancels its execution if it is running; an
        /// MCP server tab stops its server.
        /// </summary>
        /// <param name="tab">The tab to close, or null for the selected tab.</param>
        [RelayCommand]
        private void CloseQuery(DocumentTabViewModel? tab)
        {
            DocumentTabViewModel? target = tab ?? SelectedTab;

            if (target is null)
            {
                return;
            }

            if (ConfirmSaveIfModified(target) == false)
            {
                return;
            }

            RemoveTab(target);
        }

        /// <summary>
        /// Closes every tab except the given one ("Close All But This").
        /// </summary>
        /// <param name="tab">The tab to keep, or null for the selected tab.</param>
        [RelayCommand]
        private void CloseOtherQueries(DocumentTabViewModel? tab)
        {
            DocumentTabViewModel? keep = tab ?? SelectedTab;

            if (keep is null)
            {
                return;
            }

            foreach (DocumentTabViewModel other in Tabs.Where(candidate => candidate != keep).ToList())
            {
                // Show the tab being asked about; stop on Cancel like SSMS.
                if (other is QueryTabViewModel { IsModified: true })
                {
                    SelectedTab = other;
                }

                if (ConfirmSaveIfModified(other) == false)
                {
                    return;
                }

                RemoveTab(other);
            }

            SelectedTab = keep;
        }

        /// <summary>
        /// Shows the Options dialog and re-applies editor settings to all tabs when saved.
        /// </summary>
        [RelayCommand]
        private void ShowOptions()
        {
            bool saved = dialogService.ShowOptionsDialog();

            if (saved == true)
            {
                OnPropertyChanged(nameof(ShowLineNumbers));
                OnPropertyChanged(nameof(EditorFontSize));
                StatusMessage = "Options saved";
            }
        }

        /// <summary>
        /// Keeps the tabs' IsSelected flags in sync with the selection, so only the
        /// selected tab's editor is shown.
        /// </summary>
        /// <param name="oldValue">The previously selected tab.</param>
        /// <param name="newValue">The newly selected tab.</param>
        partial void OnSelectedTabChanged(DocumentTabViewModel? oldValue, DocumentTabViewModel? newValue)
        {
            if (oldValue is not null)
            {
                oldValue.IsSelected = false;
            }

            if (newValue is not null)
            {
                newValue.IsSelected = true;
            }
        }

        /// <summary>
        /// Creates a query tab, adds it after the existing tabs and selects it.
        /// </summary>
        /// <param name="database">The tab's database, or null.</param>
        /// <param name="sql">The initial SQL text.</param>
        /// <returns>The new tab.</returns>
        private QueryTabViewModel AddQueryTab(string? database, string sql)
        {
            QueryTabViewModel tab = new(
                sqlService,
                statementAnalyzer,
                optionsService,
                dialogService,
                $"{DefaultTitlePrefix}{nextQueryNumber}");

            nextQueryNumber++;

            tab.ActiveDatabase = string.IsNullOrWhiteSpace(database) == true ? null : database;
            tab.LoadText(sql);
            tab.PropertyChanged += OnTabPropertyChanged;

            AddDocumentTab(tab);

            return tab;
        }

        /// <summary>
        /// Adds a tab of any kind after the existing tabs and selects it.
        /// </summary>
        /// <param name="tab">The tab to add.</param>
        private void AddDocumentTab(DocumentTabViewModel tab)
        {
            Tabs.Add(tab);
            OnPropertyChanged(nameof(HasTabs));

            SelectedTab = tab;
        }

        /// <summary>
        /// Removes a tab without prompting, lets it release what it holds (cancel a
        /// running query, stop an MCP server), and selects the neighbouring tab when
        /// the removed one was selected.
        /// </summary>
        /// <param name="tab">The tab to remove.</param>
        private void RemoveTab(DocumentTabViewModel tab)
        {
            int index = Tabs.IndexOf(tab);

            if (index < 0)
            {
                return;
            }

            bool wasSelected = SelectedTab == tab;

            tab.OnClosed();
            tab.PropertyChanged -= OnTabPropertyChanged;
            tab.IsSelected = false;

            Tabs.RemoveAt(index);
            OnPropertyChanged(nameof(HasTabs));

            if (wasSelected == true || (SelectedTab is not null && Tabs.Contains(SelectedTab) == false))
            {
                SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
            }
        }

        /// <summary>
        /// Asks whether to save a modified tab and saves it if requested.
        /// </summary>
        /// <param name="tab">The tab about to be closed.</param>
        /// <returns>True when the tab may be closed; false when the user cancelled.</returns>
        private bool ConfirmSaveIfModified(DocumentTabViewModel tab)
        {
            // Only query tabs have content to save.
            if (tab is not QueryTabViewModel queryTab || queryTab.IsModified == false)
            {
                return true;
            }

            SaveChangesChoice choice = dialogService.ConfirmSaveChanges(queryTab.DisplayName);

            switch (choice)
            {
                case SaveChangesChoice.Save:
                {
                    return SaveTab(queryTab);
                }

                case SaveChangesChoice.Discard:
                {
                    return true;
                }

                default:
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Opens one file: activates its tab when already open, otherwise reads it into
        /// the selected tab (when that is an untouched new query) or a new tab.
        /// </summary>
        /// <param name="path">The file to open.</param>
        private void OpenSingleFile(string path)
        {
            string fullPath;

            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                dialogService.ShowError($"Could not open '{path}'.\n\n{ex.Message}", "Open File");

                return;
            }

            QueryTabViewModel? existing = QueryTabs.FirstOrDefault(
                tab => tab.FilePath is not null && string.Equals(tab.FilePath, fullPath, StringComparison.OrdinalIgnoreCase) == true);

            if (existing is not null)
            {
                SelectedTab = existing;
                StatusMessage = $"'{existing.DisplayName}' is already open";

                return;
            }

            TextFileContent content;

            try
            {
                content = fileService.ReadText(fullPath);
            }
            catch (Exception ex)
            {
                dialogService.ShowError($"Could not open '{fullPath}'.\n\n{ex.Message}", "Open File");

                return;
            }

            QueryTabViewModel target = SelectedQueryTab is not null && SelectedQueryTab.IsPristine == true
                ? SelectedQueryTab
                : AddQueryTab(SelectedQueryTab?.ActiveDatabase, string.Empty);

            target.LoadFile(fullPath, content);
            lastFileDirectory = Path.GetDirectoryName(fullPath);
            StatusMessage = $"Opened '{target.DisplayName}'";
        }

        /// <summary>
        /// Saves a tab to its file, or via Save As when it has no file yet.
        /// </summary>
        /// <param name="tab">The tab to save.</param>
        /// <returns>True when saved; false when cancelled or failed.</returns>
        private bool SaveTab(QueryTabViewModel tab)
        {
            if (tab.FilePath is null)
            {
                return SaveTabAs(tab);
            }

            return WriteTab(tab, tab.FilePath);
        }

        /// <summary>
        /// Asks for a file name and saves a tab to it.
        /// </summary>
        /// <param name="tab">The tab to save.</param>
        /// <returns>True when saved; false when cancelled or failed.</returns>
        private bool SaveTabAs(QueryTabViewModel tab)
        {
            string? path = dialogService.ShowSaveFileDialog(tab.SuggestedFileName, GetDialogDirectory(tab));

            if (path is null)
            {
                return false;
            }

            return WriteTab(tab, Path.GetFullPath(path));
        }

        /// <summary>
        /// Writes a tab's text to a file, keeping the tab's encoding (new files use
        /// the default encoding), and links the tab to that file.
        /// </summary>
        /// <param name="tab">The tab to save.</param>
        /// <param name="path">The full path to write.</param>
        /// <returns>True when saved; false when writing failed.</returns>
        private bool WriteTab(QueryTabViewModel tab, string path)
        {
            Encoding encoding = tab.FileEncoding ?? fileService.DefaultEncoding;

            try
            {
                fileService.WriteText(path, tab.EditorText, encoding);
            }
            catch (Exception ex)
            {
                dialogService.ShowError($"Could not save '{path}'.\n\n{ex.Message}", "Save File");

                return false;
            }

            tab.MarkSaved(path, encoding);
            lastFileDirectory = Path.GetDirectoryName(path);
            StatusMessage = $"Saved '{tab.DisplayName}'";

            return true;
        }

        /// <summary>
        /// Picks the folder a file dialog starts in: the tab's own file folder, else
        /// the folder of the last file opened or saved.
        /// </summary>
        /// <param name="tab">The tab the dialog is for, or null.</param>
        /// <returns>The folder, or null to let Windows decide.</returns>
        private string? GetDialogDirectory(QueryTabViewModel? tab)
        {
            if (tab?.FilePath is not null)
            {
                return Path.GetDirectoryName(tab.FilePath);
            }

            return lastFileDirectory;
        }

        /// <summary>
        /// Adds a database to the toolbar dropdown list when it is missing, keeping the
        /// list sorted by name. A tab can be in a database the list does not know yet
        /// (created after the list was loaded, or reached with USE); without this the
        /// dropdown cannot select it and keeps showing the previous tab's database.
        /// </summary>
        /// <param name="database">The database name, or null.</param>
        /// <returns>
        /// The name as it appears in the list (the listed spelling when it differs only
        /// in case), or null when no database was given.
        /// </returns>
        private string? EnsureDatabaseListed(string? database)
        {
            if (string.IsNullOrWhiteSpace(database) == true)
            {
                return null;
            }

            string? listed = Databases.FirstOrDefault(name => string.Equals(name, database, StringComparison.OrdinalIgnoreCase) == true);

            if (listed is not null)
            {
                return listed;
            }

            int index = 0;

            while (index < Databases.Count && string.Compare(Databases[index], database, StringComparison.OrdinalIgnoreCase) < 0)
            {
                index++;
            }

            Databases.Insert(index, database);

            return database;
        }

        /// <summary>
        /// Re-evaluates the Execute/Cancel buttons when a tab starts or stops executing,
        /// and makes sure a tab's new database is in the dropdown list so the dropdown
        /// can show it.
        /// </summary>
        private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(QueryTabViewModel.IsExecuting))
            {
                ExecuteQueryCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }

            if (e.PropertyName == nameof(QueryTabViewModel.ActiveDatabase) && sender is QueryTabViewModel tab)
            {
                string? listed = EnsureDatabaseListed(tab.ActiveDatabase);

                // The dropdown matches names exactly, so use the listed spelling
                // ("sales" from a USE becomes "Sales"). Setting it raises this handler
                // once more, which then finds the names equal and stops.
                if (listed is not null && string.Equals(listed, tab.ActiveDatabase, StringComparison.Ordinal) == false)
                {
                    tab.ActiveDatabase = listed;
                }
            }
        }

        /// <summary>
        /// Whether a query can be executed now.
        /// </summary>
        /// <returns>True when connected and the selected tab is not already executing.</returns>
        private bool CanExecuteQuery()
        {
            return IsConnected == true && SelectedQueryTab is not null && SelectedQueryTab.IsExecuting == false;
        }

        /// <summary>
        /// Whether a running query can be cancelled now.
        /// </summary>
        /// <returns>True while the selected tab is executing.</returns>
        private bool CanCancel()
        {
            return SelectedQueryTab is not null && SelectedQueryTab.IsExecuting == true;
        }

        /// <summary>
        /// Whether a disconnect (or starting an MCP server) is possible now.
        /// </summary>
        /// <returns>True when connected.</returns>
        private bool CanDisconnect()
        {
            return IsConnected == true;
        }

        /// <summary>
        /// Whether a query tab is selected (for query commands such as Save).
        /// </summary>
        /// <returns>True when a query tab is selected.</returns>
        private bool HasSelectedTab()
        {
            return SelectedQueryTab is not null;
        }
    }
}
