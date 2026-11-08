using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Database;
using LightSQL.Services.Llm;
using LightSQL.Services.Tools;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// A tab that chats with an LLM about one database. The model runs the same database tools
    /// as the MCP server (through the shared <see cref="DatabaseToolExecutor"/>), locked to this
    /// database and read-only until the user grants write-access. The system prompt reuses the
    /// shared instructions plus any pasted metadata for this connection + database.
    /// </summary>
    public partial class ChatTabViewModel : DocumentTabViewModel
    {
        private readonly ILlmClientFactory llmClientFactory;
        private readonly IDialogService dialogService;
        private readonly IClipboardService clipboardService;
        private readonly ISqlConnectionService sqlService;
        private readonly IMetadataStore metadataStore;
        private readonly IDatabaseProviderRegistry providerRegistry;
        private readonly ConnectionProfile profile;
        private readonly DatabaseToolExecutor executor;

        /// <summary>
        /// The UI thread's context. Tool calls may complete on a worker thread and are posted
        /// here before touching the bound transcript; null in unit tests.
        /// </summary>
        private readonly SynchronizationContext? uiContext;

        /// <summary>
        /// The current access mode; read-only until the user grants write-access. Read by the
        /// executor on every query.
        /// </summary>
        private volatile McpAccessMode accessMode = McpAccessMode.ReadOnly;

        /// <summary>
        /// The agent, created on the first message so settings and metadata are read fresh.
        /// </summary>
        private ChatAgent? agent;

        /// <summary>
        /// The tool-call panel for the message in flight. Tool calls in one turn are grouped
        /// into a single collapsible panel; this is reset to null at the start of each send.
        /// </summary>
        private ChatToolGroup? currentToolGroup;

        /// <summary>
        /// Cancels the in-flight message when the user presses Stop or closes the tab.
        /// </summary>
        private CancellationTokenSource? sendCancellation;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))]
        private string? inputText;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool isBusy;

        /// <summary>
        /// The AI provider the chat sends to; the model list follows this choice.
        /// </summary>
        [ObservableProperty]
        private LlmProviderDefinition? selectedProvider;

        /// <summary>
        /// The model identifier (editable, seeded from the provider's suggested models).
        /// </summary>
        [ObservableProperty]
        private string? selectedModel;

        /// <summary>
        /// Creates the chat tab.
        /// </summary>
        /// <param name="llmClientFactory">Creates the configured LLM client.</param>
        /// <param name="dialogService">Confirmations and the provider-settings prompt.</param>
        /// <param name="clipboardService">Clipboard access for the copy commands.</param>
        /// <param name="sqlService">The connected SQL service the tools run against.</param>
        /// <param name="statementAnalyzer">Detects data-changing statements (read-only mode).</param>
        /// <param name="metadataStore">Supplies the database metadata for the system prompt.</param>
        /// <param name="providerRegistry">Resolves the engine display name.</param>
        /// <param name="profile">The connection (a copy is kept).</param>
        /// <param name="serverName">The server / host name shown in the tab.</param>
        /// <param name="database">The single database this chat is bound to.</param>
        public ChatTabViewModel(
            ILlmClientFactory llmClientFactory,
            IDialogService dialogService,
            IClipboardService clipboardService,
            ISqlConnectionService sqlService,
            ISqlStatementAnalyzer statementAnalyzer,
            IMetadataStore metadataStore,
            IDatabaseProviderRegistry providerRegistry,
            ConnectionProfile profile,
            string serverName,
            string? database)
        {
            this.llmClientFactory = llmClientFactory;
            this.dialogService = dialogService;
            this.clipboardService = clipboardService;
            this.sqlService = sqlService;
            this.metadataStore = metadataStore;
            this.providerRegistry = providerRegistry;
            this.profile = profile.Clone();

            uiContext = SynchronizationContext.Current;
            ServerName = serverName;
            DefaultDatabase = string.IsNullOrWhiteSpace(database) == true ? null : database;

            executor = new DatabaseToolExecutor(sqlService, statementAnalyzer, DefaultDatabase, () => accessMode, OnToolCalled);

            InitializeProviderSelection();
        }

        /// <summary>
        /// The server / host name.
        /// </summary>
        public string ServerName { get; }

        /// <summary>
        /// The single database this chat is bound to, or null for the connection's default.
        /// </summary>
        public string? DefaultDatabase { get; }

        /// <summary>
        /// The chat transcript, oldest first: <see cref="ChatMessage"/> lines (user, assistant,
        /// error) and <see cref="ChatToolGroup"/> panels (the tool calls for a turn).
        /// </summary>
        public ObservableCollection<ChatTranscriptItem> Messages { get; } = new();

        /// <summary>
        /// The AI providers the user can pick from (fixed catalog).
        /// </summary>
        public IReadOnlyList<LlmProviderDefinition> Providers { get; } = LlmProviderCatalog.Providers;

        /// <summary>
        /// The suggested models for the selected provider (the model box is editable).
        /// </summary>
        public ObservableCollection<string> Models { get; } = new();

        /// <inheritdoc />
        public override string Header
        {
            get
            {
                string name = $"Chat {DefaultDatabase ?? ServerName}";

                return accessMode == McpAccessMode.ReadWrite ? $"{name} [write-access]" : name;
            }
        }

        /// <inheritdoc />
        public override string ToolTip
        {
            get
            {
                return DefaultDatabase is null
                    ? $"Chat with {ServerName}"
                    : $"Chat with {ServerName}, database {DefaultDatabase}";
            }
        }

        /// <inheritdoc />
        public override string DisplayName
        {
            get
            {
                return $"Chat {DefaultDatabase ?? ServerName}";
            }
        }

        /// <summary>
        /// Whether the chat may change data (bound to the "Allow write-access" check box).
        /// Turning it on asks for confirmation first.
        /// </summary>
        public bool IsWriteAccess
        {
            get
            {
                return accessMode == McpAccessMode.ReadWrite;
            }

            set
            {
                if (value == IsWriteAccess)
                {
                    return;
                }

                if (value == true)
                {
                    bool confirmed = dialogService.Confirm(
                        $"Allow the chat to change data in {DefaultDatabase ?? ServerName}?\n\n"
                        + "With write-access it can run INSERT, UPDATE, DELETE, DDL and stored procedures "
                        + "with the permissions of this connection's login.",
                        "Allow Write-Access");

                    if (confirmed == false)
                    {
                        // Raise anyway so the check box returns to unchecked.
                        OnPropertyChanged(nameof(IsWriteAccess));

                        return;
                    }
                }

                accessMode = value == true ? McpAccessMode.ReadWrite : McpAccessMode.ReadOnly;

                OnPropertyChanged(nameof(IsWriteAccess));
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
                return IsWriteAccess == true
                    ? "Write-access: the chat can change data"
                    : "Read-only: the chat can only read, every batch rolled back";
            }
        }

        /// <inheritdoc />
        public override void OnClosed()
        {
            sendCancellation?.Cancel();
        }

        /// <summary>
        /// Sends the typed message to the model, runs any tool calls, and shows the reply.
        /// </summary>
        /// <returns>A task that completes when the turn finishes.</returns>
        [RelayCommand(CanExecute = nameof(CanSend))]
        private async Task SendAsync()
        {
            string text = (InputText ?? string.Empty).Trim();

            if (text.Length == 0)
            {
                return;
            }

            LlmProviderDefinition? provider = SelectedProvider;
            string model = (SelectedModel ?? string.Empty).Trim();

            if (provider is null || model.Length == 0)
            {
                AddMessage(ChatAuthor.Error, "Choose an AI provider and model above first.", true);

                return;
            }

            if (llmClientFactory.IsConfigured(provider.Provider) == false)
            {
                bool configure = dialogService.Confirm(
                    $"No API key is set for {provider.DisplayName}. Open the AI Provider settings now?",
                    "Chat");

                if (configure == true)
                {
                    dialogService.ShowLlmSettingsDialog();
                }

                return;
            }

            llmClientFactory.RememberSelection(provider.Provider, model);

            InputText = string.Empty;
            AddMessage(ChatAuthor.User, text, false);
            IsBusy = true;
            currentToolGroup = null;
            sendCancellation = new CancellationTokenSource();

            try
            {
                ChatAgent chatAgent = EnsureAgent(provider.Provider, model);
                string answer = await chatAgent.SendAsync(text, sendCancellation.Token);

                AddMessage(ChatAuthor.Assistant, answer, false);
            }
            catch (OperationCanceledException)
            {
                AddMessage(ChatAuthor.Assistant, "(stopped)", false);
            }
            catch (Exception ex)
            {
                AddMessage(ChatAuthor.Error, ex.Message, true);
            }
            finally
            {
                IsBusy = false;
                sendCancellation?.Dispose();
                sendCancellation = null;
            }
        }

        /// <summary>
        /// Whether a message can be sent now (not busy and the input is not empty).
        /// </summary>
        /// <returns>True when a message can be sent.</returns>
        private bool CanSend()
        {
            return IsBusy == false && string.IsNullOrWhiteSpace(InputText) == false;
        }

        /// <summary>
        /// Cancels the in-flight message.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStop))]
        private void Stop()
        {
            sendCancellation?.Cancel();
        }

        /// <summary>
        /// Whether a message is in flight and can be stopped.
        /// </summary>
        /// <returns>True while busy.</returns>
        private bool CanStop()
        {
            return IsBusy == true;
        }

        /// <summary>
        /// Creates the agent on first use (building the system prompt from the shared instructions
        /// and this connection + database's pasted metadata), or swaps its LLM backend when the
        /// user has since changed the provider or model. The conversation history is kept.
        /// </summary>
        /// <param name="provider">The selected provider.</param>
        /// <param name="model">The selected model.</param>
        /// <returns>The agent.</returns>
        private ChatAgent EnsureAgent(LlmProvider provider, string model)
        {
            ILlmChatClient client = llmClientFactory.Create(provider, model);

            if (agent is null)
            {
                string engineName = providerRegistry.Get(profile.Engine).DisplayName;
                ConnectionMetadata? metadata = metadataStore.Get(profile.Name, DefaultDatabase);
                string systemPrompt = DatabaseAssistantPrompt.Build(engineName, sqlService.ServerName ?? ServerName, DefaultDatabase, metadata?.Json);

                agent = new ChatAgent(client, executor, systemPrompt);
            }
            else
            {
                agent.UseClient(client);
            }

            return agent;
        }

        /// <summary>
        /// Picks the starting provider and model for a new chat tab: the provider the user chose
        /// last, otherwise the first provider that has a saved key, otherwise the first in the
        /// catalog. Setting the provider populates the model list (see
        /// <see cref="OnSelectedProviderChanged"/>).
        /// </summary>
        private void InitializeProviderSelection()
        {
            LlmProviderDefinition? initial = null;

            if (llmClientFactory.LastProvider is not null)
            {
                initial = LlmProviderCatalog.Get(llmClientFactory.LastProvider.Value);
            }

            initial ??= Providers.FirstOrDefault(definition =>
                definition.RequiresApiKey == true && llmClientFactory.IsConfigured(definition.Provider) == true);

            initial ??= Providers.FirstOrDefault();

            SelectedProvider = initial;
        }

        /// <summary>
        /// Refreshes the model list when the provider changes, defaulting the model to the one the
        /// user used last (if it is this provider's) or the provider's first suggestion.
        /// </summary>
        /// <param name="value">The newly selected provider.</param>
        partial void OnSelectedProviderChanged(LlmProviderDefinition? value)
        {
            Models.Clear();

            if (value is null)
            {
                SelectedModel = null;

                return;
            }

            foreach (string model in value.Models)
            {
                Models.Add(model);
            }

            if (llmClientFactory.LastProvider == value.Provider && string.IsNullOrWhiteSpace(llmClientFactory.LastModel) == false)
            {
                SelectedModel = llmClientFactory.LastModel;
            }
            else
            {
                SelectedModel = value.Models.Count > 0 ? value.Models[0] : null;
            }
        }

        /// <summary>
        /// Adds a line to the transcript (must run on the UI thread).
        /// </summary>
        /// <param name="author">Who produced the line.</param>
        /// <param name="text">The text.</param>
        /// <param name="isError">Whether it is an error.</param>
        private void AddMessage(ChatAuthor author, string text, bool isError)
        {
            Messages.Add(new ChatMessage { Author = author, Text = text, IsError = isError });
            CopyConversationCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Shows a tool call in the transcript, marshalled to the UI thread.
        /// </summary>
        /// <param name="call">The tool call.</param>
        private void OnToolCalled(McpToolCall call)
        {
            if (uiContext is null || SynchronizationContext.Current == uiContext)
            {
                AddToolCall(call);
            }
            else
            {
                uiContext.Post(_ => AddToolCall(call), null);
            }
        }

        /// <summary>
        /// Adds a tool call to the current turn's collapsible panel, creating the panel the first
        /// time a tool runs in the turn.
        /// </summary>
        /// <param name="call">The tool call.</param>
        private void AddToolCall(McpToolCall call)
        {
            string detail = string.IsNullOrWhiteSpace(call.Outcome) == true
                ? call.ToolName
                : $"{call.ToolName}: {call.Outcome}";

            if (currentToolGroup is null)
            {
                currentToolGroup = new ChatToolGroup();
                Messages.Add(currentToolGroup);
            }

            currentToolGroup.Add(new ChatToolCall
            {
                ToolName = call.ToolName,
                Detail = detail,
                IsError = call.IsSuccess == false
            });
        }

        /// <summary>
        /// Copies the whole conversation (user, assistant and error lines, without the tool
        /// calls) to the clipboard.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCopyConversation))]
        private void CopyConversation()
        {
            clipboardService.SetText(BuildConversationText());
        }

        /// <summary>
        /// Whether there is any message to copy (tool-only turns do not count).
        /// </summary>
        /// <returns>True when the transcript has at least one message.</returns>
        private bool CanCopyConversation()
        {
            return Messages.OfType<ChatMessage>().Any();
        }

        /// <summary>
        /// Copies a single message's text to the clipboard.
        /// </summary>
        /// <param name="message">The message to copy (ignored when null).</param>
        [RelayCommand]
        private void CopyMessage(ChatMessage? message)
        {
            if (message is null)
            {
                return;
            }

            clipboardService.SetText(message.Text);
        }

        /// <summary>
        /// Builds the plain-text form of the conversation, skipping the tool-call panels.
        /// </summary>
        /// <returns>The conversation text.</returns>
        private string BuildConversationText()
        {
            StringBuilder builder = new();

            foreach (ChatMessage message in Messages.OfType<ChatMessage>())
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder.AppendLine(AuthorLabel(message.Author));
                builder.Append(message.Text);
            }

            return builder.ToString();
        }

        /// <summary>
        /// The label shown before a message when the conversation is copied.
        /// </summary>
        /// <param name="author">The message author.</param>
        /// <returns>The label.</returns>
        private static string AuthorLabel(ChatAuthor author)
        {
            return author switch
            {
                ChatAuthor.User => "User:",
                ChatAuthor.Assistant => "Assistant:",
                ChatAuthor.Error => "Error:",
                _ => $"{author}:"
            };
        }
    }
}
