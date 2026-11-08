using System.Linq;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Database;
using LightSQL.Services.Llm;
using LightSQL.ViewModels;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the chat tab: sending a message, running a tool call (shown in the transcript),
    /// prompting when no provider is configured, the send gate, and the write-access toggle.
    /// </summary>
    public class ChatTabViewModelTests
    {
        private readonly FakeLlmClientFactory factory = new();
        private readonly FakeDialogService dialogService = new();
        private readonly FakeClipboardService clipboardService = new();
        private readonly FakeSqlConnectionService sqlService = new() { IsConnected = true, CurrentDatabase = "master" };

        [Fact]
        public async Task Send_ShowsUserAndAssistantMessages()
        {
            factory.Client.Enqueue(new LlmResponse { Text = "Hi there" });
            ChatTabViewModel tab = CreateTab();
            tab.InputText = "hello";

            await tab.SendCommand.ExecuteAsync(null);

            Assert.Contains(tab.Messages.OfType<ChatMessage>(), message => message.Author == ChatAuthor.User && message.Text == "hello");
            Assert.Contains(tab.Messages.OfType<ChatMessage>(), message => message.Author == ChatAuthor.Assistant && message.Text == "Hi there");
            Assert.Equal(string.Empty, tab.InputText);
            Assert.False(tab.IsBusy);
        }

        [Fact]
        public async Task Send_RunsToolCall_ShowsItInACollapsedPanel()
        {
            sqlService.Tables.Add(new SchemaObject { Schema = "dbo", Name = "Orders" });
            factory.Client.Enqueue(new LlmResponse { ToolCalls = new[] { new LlmToolCall { Id = "c1", Name = "list_tables", ArgumentsJson = "{}" } } });
            factory.Client.Enqueue(new LlmResponse { Text = "There is one table." });
            ChatTabViewModel tab = CreateTab();
            tab.InputText = "what tables?";

            await tab.SendCommand.ExecuteAsync(null);

            ChatToolGroup group = Assert.Single(tab.Messages.OfType<ChatToolGroup>());
            Assert.False(group.IsExpanded);
            Assert.Equal("list_tables", group.Title);
            Assert.Contains(group.Calls, call => call.ToolName == "list_tables");
            Assert.Contains(tab.Messages.OfType<ChatMessage>(), message => message.Author == ChatAuthor.Assistant && message.Text == "There is one table.");
        }

        [Fact]
        public async Task Send_WhenNotConfigured_PromptsToConfigure_AndSendsNothing()
        {
            factory.IsConfiguredResult = false;
            dialogService.ConfirmResult = false;
            ChatTabViewModel tab = CreateTab();
            tab.InputText = "hello";

            await tab.SendCommand.ExecuteAsync(null);

            Assert.Equal(1, dialogService.ConfirmCount);
            Assert.Empty(tab.Messages);
        }

        [Fact]
        public void CanSend_RequiresNonEmptyInput()
        {
            ChatTabViewModel tab = CreateTab();

            Assert.False(tab.SendCommand.CanExecute(null));

            tab.InputText = "hi";

            Assert.True(tab.SendCommand.CanExecute(null));
        }

        [Fact]
        public void EnablingWriteAccess_Declined_StaysReadOnly()
        {
            dialogService.ConfirmResult = false;
            ChatTabViewModel tab = CreateTab();

            tab.IsWriteAccess = true;

            Assert.False(tab.IsWriteAccess);
        }

        [Fact]
        public void NewTab_SelectsAProviderAndModel()
        {
            ChatTabViewModel tab = CreateTab();

            Assert.NotNull(tab.SelectedProvider);
            Assert.False(string.IsNullOrWhiteSpace(tab.SelectedModel));
            Assert.NotEmpty(tab.Models);
        }

        [Fact]
        public void ChangingProvider_RepopulatesModels_AndPicksTheFirst()
        {
            ChatTabViewModel tab = CreateTab();
            LlmProviderDefinition openAi = tab.Providers.Single(provider => provider.Provider == LlmProvider.OpenAI);

            tab.SelectedProvider = openAi;

            Assert.Equal(openAi.Models, tab.Models);
            Assert.Equal(openAi.Models[0], tab.SelectedModel);
        }

        [Fact]
        public async Task Send_RemembersTheSelection()
        {
            factory.Client.Enqueue(new LlmResponse { Text = "ok" });
            ChatTabViewModel tab = CreateTab();
            tab.InputText = "hello";

            await tab.SendCommand.ExecuteAsync(null);

            Assert.Equal(tab.SelectedProvider!.Provider, factory.RememberedProvider);
            Assert.Equal(tab.SelectedModel, factory.RememberedModel);
        }

        [Fact]
        public async Task CopyConversation_CopiesMessages_WithoutToolCalls()
        {
            sqlService.Tables.Add(new SchemaObject { Schema = "dbo", Name = "Orders" });
            factory.Client.Enqueue(new LlmResponse { ToolCalls = new[] { new LlmToolCall { Id = "c1", Name = "list_tables", ArgumentsJson = "{}" } } });
            factory.Client.Enqueue(new LlmResponse { Text = "There is one table." });
            ChatTabViewModel tab = CreateTab();
            tab.InputText = "what tables?";
            await tab.SendCommand.ExecuteAsync(null);

            tab.CopyConversationCommand.Execute(null);

            Assert.NotNull(clipboardService.Text);
            Assert.Contains("what tables?", clipboardService.Text);
            Assert.Contains("There is one table.", clipboardService.Text);
            Assert.DoesNotContain("list_tables", clipboardService.Text);
        }

        [Fact]
        public void CopyConversation_IsDisabled_WhenThereAreNoMessages()
        {
            ChatTabViewModel tab = CreateTab();

            Assert.False(tab.CopyConversationCommand.CanExecute(null));
        }

        [Fact]
        public async Task CopyMessage_CopiesThatMessageText()
        {
            factory.Client.Enqueue(new LlmResponse { Text = "Hi there" });
            ChatTabViewModel tab = CreateTab();
            tab.InputText = "hello";
            await tab.SendCommand.ExecuteAsync(null);
            ChatMessage reply = tab.Messages.OfType<ChatMessage>().Single(message => message.Author == ChatAuthor.Assistant);

            tab.CopyMessageCommand.Execute(reply);

            Assert.Equal("Hi there", clipboardService.Text);
        }

        /// <summary>
        /// Creates a chat tab over the fakes, bound to the "Sales" database.
        /// </summary>
        /// <returns>The tab.</returns>
        private ChatTabViewModel CreateTab()
        {
            ConnectionProfile profile = new() { Name = "conn1", Server = "sql01", Engine = DatabaseEngine.SqlServer };

            return new ChatTabViewModel(
                factory,
                dialogService,
                clipboardService,
                sqlService,
                new SqlStatementAnalyzer(),
                new FakeMetadataStore(),
                new DatabaseProviderRegistry(),
                profile,
                "SQL01",
                "Sales");
        }
    }
}
