using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// The main window's view model. Orchestrates the connection, the query tabs
    /// (SSMS-style documents backed by .sql files: New, Open, Save, Save As),
    /// options and the Object Explorer.
    /// </summary>
    public partial class MainViewModel : ObservableObject, IMainActions
    {
        private readonly ISqlConnectionService sqlService;
        private readonly ISqlStatementAnalyzer statementAnalyzer;
        private readonly IFileService fileService;
        private readonly IOptionsService optionsService;
        private readonly IDialogService dialogService;

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
        private bool isConnected;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExecuteQueryCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveFileCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveFileAsCommand))]
        private QueryTabViewModel? selectedTab;

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
        public MainViewModel(
            ISqlConnectionService sqlService,
            ISqlStatementAnalyzer statementAnalyzer,
            IFileService fileService,
            IOptionsService optionsService,
            IDialogService dialogService,
            ObjectExplorerViewModel objectExplorer)
        {
            this.sqlService = sqlService;
            this.statementAnalyzer = statementAnalyzer;
            this.fileService = fileService;
            this.optionsService = optionsService;
            this.dialogService = dialogService;

            ObjectExplorer = objectExplorer;
            ObjectExplorer.MainActions = this;

            // Start with one empty query, like SSMS's "New Query".
            AddTab(null, string.Empty);
        }

        /// <summary>
        /// The Object Explorer tree.
        /// </summary>
        public ObjectExplorerViewModel ObjectExplorer { get; }

        /// <summary>
        /// The open query tabs, in display order.
        /// </summary>
        public ObservableCollection<QueryTabViewModel> Tabs { get; } = new();

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
            AddTab(EnsureDatabaseListed(database), sql);
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
            if (SelectedTab is null)
            {
                return Task.CompletedTask;
            }

            return SelectedTab.ExecuteAsync();
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
                Dictionary<QueryTabViewModel, string?> previous = Tabs.ToDictionary(tab => tab, tab => tab.ActiveDatabase);

                Databases.Clear();

                foreach (string database in databases)
                {
                    Databases.Add(database);
                }

                string? current = sqlService.CurrentDatabase;
                string? fallback = current is not null && Databases.Contains(current) == true
                    ? current
                    : Databases.FirstOrDefault();

                foreach (QueryTabViewModel tab in Tabs)
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
            foreach (QueryTabViewModel tab in Tabs.ToList())
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
        /// Disconnects from the current server. Open tabs stay open but lose their database.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDisconnect))]
        private void Disconnect()
        {
            sqlService.Disconnect();
            IsConnected = false;
            ConnectionStatus = "Not connected";
            Databases.Clear();

            foreach (QueryTabViewModel tab in Tabs)
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
            SelectedTab?.CancelExecution();
        }

        /// <summary>
        /// Opens a new, empty query tab using the selected tab's database.
        /// </summary>
        [RelayCommand]
        private void NewQuery()
        {
            string? database = SelectedTab?.ActiveDatabase;

            if (database is null && IsConnected == true)
            {
                database = sqlService.CurrentDatabase;
            }

            AddTab(database, string.Empty);
            StatusMessage = "New query";
        }

        /// <summary>
        /// Shows the Open dialog and opens the chosen .sql files in tabs (File → Open).
        /// </summary>
        [RelayCommand]
        private void OpenFile()
        {
            IReadOnlyList<string> paths = dialogService.ShowOpenFileDialog(GetDialogDirectory(SelectedTab));

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
            QueryTabViewModel? target = tab ?? SelectedTab;

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
            QueryTabViewModel? target = tab ?? SelectedTab;

            if (target is not null)
            {
                SaveTabAs(target);
            }
        }

        /// <summary>
        /// Closes a query tab (the selected one when none is given), asking to save
        /// unsaved changes first and cancelling its execution if it is running.
        /// </summary>
        /// <param name="tab">The tab to close, or null for the selected tab.</param>
        [RelayCommand]
        private void CloseQuery(QueryTabViewModel? tab)
        {
            QueryTabViewModel? target = tab ?? SelectedTab;

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
        private void CloseOtherQueries(QueryTabViewModel? tab)
        {
            QueryTabViewModel? keep = tab ?? SelectedTab;

            if (keep is null)
            {
                return;
            }

            foreach (QueryTabViewModel other in Tabs.Where(candidate => candidate != keep).ToList())
            {
                // Show the tab being asked about; stop on Cancel like SSMS.
                if (other.IsModified == true)
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
        partial void OnSelectedTabChanged(QueryTabViewModel? oldValue, QueryTabViewModel? newValue)
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
        /// Creates a tab, adds it after the existing tabs and selects it.
        /// </summary>
        /// <param name="database">The tab's database, or null.</param>
        /// <param name="sql">The initial SQL text.</param>
        /// <returns>The new tab.</returns>
        private QueryTabViewModel AddTab(string? database, string sql)
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

            Tabs.Add(tab);
            OnPropertyChanged(nameof(HasTabs));

            SelectedTab = tab;

            return tab;
        }

        /// <summary>
        /// Removes a tab without prompting, cancelling its execution, and selects the
        /// neighbouring tab when the removed one was selected.
        /// </summary>
        /// <param name="tab">The tab to remove.</param>
        private void RemoveTab(QueryTabViewModel tab)
        {
            int index = Tabs.IndexOf(tab);

            if (index < 0)
            {
                return;
            }

            bool wasSelected = SelectedTab == tab;

            tab.CancelExecution();
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
        private bool ConfirmSaveIfModified(QueryTabViewModel tab)
        {
            if (tab.IsModified == false)
            {
                return true;
            }

            SaveChangesChoice choice = dialogService.ConfirmSaveChanges(tab.DisplayName);

            switch (choice)
            {
                case SaveChangesChoice.Save:
                {
                    return SaveTab(tab);
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

            QueryTabViewModel? existing = Tabs.FirstOrDefault(
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

            QueryTabViewModel target = SelectedTab is not null && SelectedTab.IsPristine == true
                ? SelectedTab
                : AddTab(SelectedTab?.ActiveDatabase, string.Empty);

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
            return IsConnected == true && SelectedTab is not null && SelectedTab.IsExecuting == false;
        }

        /// <summary>
        /// Whether a running query can be cancelled now.
        /// </summary>
        /// <returns>True while the selected tab is executing.</returns>
        private bool CanCancel()
        {
            return SelectedTab is not null && SelectedTab.IsExecuting == true;
        }

        /// <summary>
        /// Whether a disconnect is possible now.
        /// </summary>
        /// <returns>True when connected.</returns>
        private bool CanDisconnect()
        {
            return IsConnected == true;
        }

        /// <summary>
        /// Whether a tab is selected (for tab-level commands such as Save).
        /// </summary>
        /// <returns>True when a tab is selected.</returns>
        private bool HasSelectedTab()
        {
            return SelectedTab is not null;
        }
    }
}
