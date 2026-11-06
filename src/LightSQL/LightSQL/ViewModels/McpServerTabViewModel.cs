using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Mcp;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// A tab that runs an MCP (Model Context Protocol) server for a connection, so
    /// AI clients such as Claude Code or Codex can explore the database and run
    /// queries from a chat. Shows which connection and database the server uses,
    /// its port and endpoint, ready-to-copy client setup, and a log of every tool
    /// call. The server starts read-only; the user can allow read-write access
    /// (after a confirmation) and switch back at any time, even while it runs.
    /// Closing the tab stops the server.
    /// </summary>
    public partial class McpServerTabViewModel : DocumentTabViewModel
    {
        private readonly IMcpServerHostFactory hostFactory;
        private readonly IClipboardService clipboardService;
        private readonly IDialogService dialogService;
        private readonly ConnectionProfile profile;

        /// <summary>
        /// The current access mode. Read by server threads on every query, so it is
        /// volatile; read-only until the user allows writes.
        /// </summary>
        private volatile McpAccessMode accessMode = McpAccessMode.ReadOnly;

        /// <summary>
        /// The UI thread's context. Tool calls arrive on server threads and are
        /// posted here before touching bound collections; null in unit tests.
        /// </summary>
        private readonly SynchronizationContext? uiContext;

        /// <summary>
        /// The running server, or null while starting, stopped or failed.
        /// </summary>
        private IMcpServerHost? host;

        /// <summary>
        /// Cancels a start that is still connecting when the tab is closed.
        /// </summary>
        private CancellationTokenSource? startCancellation;

        /// <summary>
        /// Set when the tab is closed; a server that finishes starting afterwards is
        /// stopped at once.
        /// </summary>
        private bool isClosed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Header))]
        [NotifyPropertyChangedFor(nameof(ToolTip))]
        [NotifyPropertyChangedFor(nameof(IsRunning))]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyCanExecuteChangedFor(nameof(StartServerCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopServerCommand))]
        [NotifyCanExecuteChangedFor(nameof(CopyEndpointCommand))]
        [NotifyCanExecuteChangedFor(nameof(CopyClaudeCommandCommand))]
        [NotifyCanExecuteChangedFor(nameof(CopyCodexConfigCommand))]
        private McpServerState state = McpServerState.Starting;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Header))]
        [NotifyPropertyChangedFor(nameof(ClientName))]
        [NotifyPropertyChangedFor(nameof(ClaudeCommand))]
        [NotifyPropertyChangedFor(nameof(CodexConfig))]
        private int? port;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ToolTip))]
        [NotifyPropertyChangedFor(nameof(ClaudeCommand))]
        [NotifyPropertyChangedFor(nameof(CodexConfig))]
        private string? endpoint;

        [ObservableProperty]
        private DateTime? startedAt;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        private string? errorMessage;

        [ObservableProperty]
        private int callCount;

        [ObservableProperty]
        private string? copyMessage;

        /// <summary>
        /// The most tool calls kept in the activity log; older ones are dropped.
        /// </summary>
        public const int MaxActivityEntries = 500;

        /// <summary>
        /// Prefix of the server name suggested for AI client configuration.
        /// </summary>
        private const string ClientNamePrefix = "lightsql";

        /// <summary>
        /// Creates the tab. Call <see cref="StartAsync"/> to start the server.
        /// </summary>
        /// <param name="hostFactory">Starts the MCP server.</param>
        /// <param name="clipboardService">Copies the client setup to the clipboard.</param>
        /// <param name="dialogService">Asks for confirmation before allowing writes.</param>
        /// <param name="profile">The connection the server uses (a copy is kept).</param>
        /// <param name="serverName">The SQL Server instance name shown in the tab.</param>
        /// <param name="defaultDatabase">The database used when a tool call names none; null for the connection's default.</param>
        public McpServerTabViewModel(
            IMcpServerHostFactory hostFactory,
            IClipboardService clipboardService,
            IDialogService dialogService,
            ConnectionProfile profile,
            string serverName,
            string? defaultDatabase)
        {
            this.hostFactory = hostFactory;
            this.clipboardService = clipboardService;
            this.dialogService = dialogService;
            this.profile = profile.Clone();

            uiContext = SynchronizationContext.Current;

            ServerName = serverName;
            DefaultDatabase = string.IsNullOrWhiteSpace(defaultDatabase) == true ? null : defaultDatabase;
        }

        /// <summary>
        /// The SQL Server instance the server exposes.
        /// </summary>
        public string ServerName { get; }

        /// <summary>
        /// The database used when a tool call names none; null for the connection's default.
        /// </summary>
        public string? DefaultDatabase { get; }

        /// <summary>
        /// The default database for display.
        /// </summary>
        public string DefaultDatabaseText
        {
            get
            {
                return DefaultDatabase ?? "(connection default)";
            }
        }

        /// <summary>
        /// How the server signs in to SQL Server, for display (never the password).
        /// </summary>
        public string AuthenticationText
        {
            get
            {
                if (profile.UseRawConnectionString == true)
                {
                    return "Connection string";
                }

                return profile.AuthenticationType == AuthenticationType.SqlServer
                    ? $"SQL Server login '{profile.UserId}'"
                    : "Windows authentication";
            }
        }

        /// <summary>
        /// The current access mode (read-only by default).
        /// </summary>
        public McpAccessMode AccessMode
        {
            get
            {
                return accessMode;
            }
        }

        /// <summary>
        /// Whether AI clients may change data (bound to the "Allow read-write" check
        /// box). Turning it on asks for confirmation first; when the user declines,
        /// the server stays read-only and the check box is reset. Takes effect from
        /// the next query, without restarting the server.
        /// </summary>
        public bool IsReadWrite
        {
            get
            {
                return accessMode == McpAccessMode.ReadWrite;
            }

            set
            {
                if (value == IsReadWrite)
                {
                    return;
                }

                if (value == true)
                {
                    bool confirmed = dialogService.Confirm(
                        $"Allow AI clients of the MCP server for {ServerName} to change data?\n\n"
                        + "In read-write mode they can run INSERT, UPDATE, DELETE, DDL and stored procedures "
                        + "with the permissions of this connection's login.",
                        "Allow Read-Write Access");

                    if (confirmed == false)
                    {
                        // Raise anyway so the check box goes back to unchecked.
                        OnPropertyChanged(nameof(IsReadWrite));

                        return;
                    }
                }

                accessMode = value == true ? McpAccessMode.ReadWrite : McpAccessMode.ReadOnly;

                OnPropertyChanged(nameof(IsReadWrite));
                OnPropertyChanged(nameof(AccessMode));
                OnPropertyChanged(nameof(AccessText));
                OnPropertyChanged(nameof(Header));
            }
        }

        /// <summary>
        /// The access mode for display.
        /// </summary>
        public string AccessText
        {
            get
            {
                return IsReadWrite == true
                    ? "Read-write: AI clients can change data"
                    : "Read-only: only reading queries, every batch rolled back";
            }
        }

        /// <summary>
        /// The tool calls made by AI clients, newest first.
        /// </summary>
        public ObservableCollection<McpToolCall> Activity { get; } = new();

        /// <summary>
        /// Whether the server is listening.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return State == McpServerState.Running;
            }
        }

        /// <summary>
        /// The server state for display.
        /// </summary>
        public string StatusText
        {
            get
            {
                switch (State)
                {
                    case McpServerState.Starting:
                    {
                        return "Starting...";
                    }

                    case McpServerState.Running:
                    {
                        return "Running";
                    }

                    case McpServerState.Stopped:
                    {
                        return "Stopped";
                    }

                    default:
                    {
                        return $"Failed: {ErrorMessage}";
                    }
                }
            }
        }

        /// <summary>
        /// The name suggested for this server in an AI client's configuration,
        /// e.g. "lightsql-9000".
        /// </summary>
        public string ClientName
        {
            get
            {
                return Port is null ? ClientNamePrefix : $"{ClientNamePrefix}-{Port}";
            }
        }

        /// <summary>
        /// The Claude Code command that registers this server.
        /// </summary>
        public string ClaudeCommand
        {
            get
            {
                return Endpoint is null ? string.Empty : $"claude mcp add --transport http {ClientName} {Endpoint}";
            }
        }

        /// <summary>
        /// The Codex configuration (~/.codex/config.toml) that registers this server.
        /// </summary>
        public string CodexConfig
        {
            get
            {
                return Endpoint is null ? string.Empty : $"[mcp_servers.{ClientName}]{Environment.NewLine}url = \"{Endpoint}\"";
            }
        }

        /// <inheritdoc />
        public override string DisplayName
        {
            get
            {
                return $"MCP {ServerName}";
            }
        }

        /// <inheritdoc />
        public override string Header
        {
            get
            {
                string name = Port is null ? DisplayName : $"{DisplayName} :{Port}";

                // Make write access visible on the tab itself.
                if (IsReadWrite == true)
                {
                    name += " [read-write]";
                }

                if (State == McpServerState.Stopped)
                {
                    return $"{name} (stopped)";
                }

                if (State == McpServerState.Failed)
                {
                    return $"{name} (failed)";
                }

                return name;
            }
        }

        /// <inheritdoc />
        public override string ToolTip
        {
            get
            {
                return Endpoint is null ? $"MCP server for {ServerName}" : $"MCP server for {ServerName} at {Endpoint}";
            }
        }

        /// <summary>
        /// Connects and starts the MCP server on a free port from 9000 up. Failures
        /// are shown in the tab rather than thrown.
        /// </summary>
        /// <returns>A task that completes when the server is running or has failed.</returns>
        public async Task StartAsync()
        {
            if (State == McpServerState.Running || isClosed == true)
            {
                return;
            }

            State = McpServerState.Starting;
            ErrorMessage = null;
            startCancellation = new CancellationTokenSource();

            McpServerSettings settings = new()
            {
                Profile = profile,
                DefaultDatabase = DefaultDatabase,
                GetAccessMode = () => accessMode,
                ToolCalled = OnToolCalled
            };

            try
            {
                IMcpServerHost started = await hostFactory.StartAsync(settings, startCancellation.Token);

                if (isClosed == true)
                {
                    // The tab was closed while the server was starting.
                    await started.StopAsync();
                    State = McpServerState.Stopped;

                    return;
                }

                host = started;
                Port = started.Port;
                Endpoint = started.Endpoint;
                StartedAt = DateTime.Now;
                State = McpServerState.Running;
            }
            catch (OperationCanceledException)
            {
                State = McpServerState.Stopped;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                State = McpServerState.Failed;
            }
            finally
            {
                startCancellation?.Dispose();
                startCancellation = null;
            }
        }

        /// <summary>
        /// Stops the server and frees its port. The tab stays open and can start it again.
        /// </summary>
        /// <returns>A task that completes when the server has stopped.</returns>
        public async Task StopAsync()
        {
            startCancellation?.Cancel();

            IMcpServerHost? running = host;
            host = null;

            if (State == McpServerState.Running)
            {
                State = McpServerState.Stopped;
            }

            if (running is null)
            {
                return;
            }

            try
            {
                await running.StopAsync();
            }
            catch (Exception ex)
            {
                // The port is released when the process ends anyway; just report it.
                ErrorMessage = ex.Message;
            }
        }

        /// <summary>
        /// Stops the server when the tab is closed.
        /// </summary>
        public override void OnClosed()
        {
            isClosed = true;

            _ = StopAsync();
        }

        /// <summary>
        /// Adds a tool call to the top of the activity log (keeping at most
        /// <see cref="MaxActivityEntries"/>) and counts it. Must run on the UI thread.
        /// </summary>
        /// <param name="call">The tool call.</param>
        public void AddActivity(McpToolCall call)
        {
            Activity.Insert(0, call);
            CallCount++;

            while (Activity.Count > MaxActivityEntries)
            {
                Activity.RemoveAt(Activity.Count - 1);
            }
        }

        /// <summary>
        /// Starts a stopped or failed server again (it may get a different port).
        /// </summary>
        /// <returns>A task that completes when the start finishes.</returns>
        [RelayCommand(CanExecute = nameof(CanStart))]
        private Task StartServerAsync()
        {
            return StartAsync();
        }

        /// <summary>
        /// Stops the running server.
        /// </summary>
        /// <returns>A task that completes when the server has stopped.</returns>
        [RelayCommand(CanExecute = nameof(CanStop))]
        private Task StopServerAsync()
        {
            return StopAsync();
        }

        /// <summary>
        /// Copies the endpoint URL.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCopy))]
        private void CopyEndpoint()
        {
            Copy(Endpoint, "Endpoint URL copied.");
        }

        /// <summary>
        /// Copies the Claude Code registration command.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCopy))]
        private void CopyClaudeCommand()
        {
            Copy(ClaudeCommand, "Claude Code command copied. Run it in a terminal.");
        }

        /// <summary>
        /// Copies the Codex configuration snippet.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCopy))]
        private void CopyCodexConfig()
        {
            Copy(CodexConfig, "Codex configuration copied. Paste it into ~/.codex/config.toml.");
        }

        /// <summary>
        /// Clears the activity log (the call counter keeps counting).
        /// </summary>
        [RelayCommand]
        private void ClearActivity()
        {
            Activity.Clear();
        }

        /// <summary>
        /// Copies text to the clipboard and confirms it in the tab.
        /// </summary>
        /// <param name="text">The text to copy.</param>
        /// <param name="confirmation">The confirmation shown in the tab.</param>
        private void Copy(string? text, string confirmation)
        {
            if (string.IsNullOrEmpty(text) == true)
            {
                return;
            }

            clipboardService.SetText(text);
            CopyMessage = confirmation;
        }

        /// <summary>
        /// Receives a tool call from a server thread and adds it to the log on the
        /// UI thread.
        /// </summary>
        /// <param name="call">The tool call.</param>
        private void OnToolCalled(McpToolCall call)
        {
            // Already on the UI thread (or there is none): add it directly.
            if (uiContext is null || SynchronizationContext.Current == uiContext)
            {
                AddActivity(call);

                return;
            }

            uiContext.Post(_ => AddActivity(call), null);
        }

        /// <summary>
        /// Whether the server can be started again.
        /// </summary>
        /// <returns>True when stopped or failed.</returns>
        private bool CanStart()
        {
            return State == McpServerState.Stopped || State == McpServerState.Failed;
        }

        /// <summary>
        /// Whether the server can be stopped.
        /// </summary>
        /// <returns>True while running.</returns>
        private bool CanStop()
        {
            return State == McpServerState.Running;
        }

        /// <summary>
        /// Whether the client setup can be copied.
        /// </summary>
        /// <returns>True while running (the endpoint is known and reachable).</returns>
        private bool CanCopy()
        {
            return State == McpServerState.Running && Endpoint is not null;
        }
    }
}
