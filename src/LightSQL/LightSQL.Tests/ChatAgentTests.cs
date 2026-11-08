using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Llm;
using LightSQL.Services.Tools;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the chat agent loop: direct answers, running a tool call and feeding the result
    /// back, surfacing read-only rejections to the model, and the iteration cap.
    /// </summary>
    public class ChatAgentTests
    {
        private readonly FakeSqlConnectionService sqlService = new() { IsConnected = true, CurrentDatabase = "master" };
        private readonly FakeLlmChatClient client = new();

        private McpAccessMode accessMode = McpAccessMode.ReadOnly;

        [Fact]
        public async Task SendAsync_DirectAnswer_ReturnsText()
        {
            client.Enqueue(new LlmResponse { Text = "Hello!" });
            ChatAgent agent = CreateAgent();

            string answer = await agent.SendAsync("hi", CancellationToken.None);

            Assert.Equal("Hello!", answer);
        }

        [Fact]
        public async Task SendAsync_RunsToolCall_ThenReturnsFinalAnswer()
        {
            sqlService.Tables.Add(new SchemaObject { Schema = "dbo", Name = "Orders" });
            client.Enqueue(new LlmResponse { ToolCalls = new[] { new LlmToolCall { Id = "c1", Name = "list_tables", ArgumentsJson = "{}" } } });
            client.Enqueue(new LlmResponse { Text = "There is one table: Orders." });
            ChatAgent agent = CreateAgent();

            string answer = await agent.SendAsync("what tables?", CancellationToken.None);

            Assert.Equal("There is one table: Orders.", answer);

            LlmRequest second = client.Requests[1];
            Assert.Contains(second.Messages, message => message.Role == LlmRole.Tool && message.ToolCallId == "c1");
            Assert.Contains(second.Messages, message => message.Role == LlmRole.Tool && message.Text!.Contains("Orders"));
        }

        [Fact]
        public async Task SendAsync_ReadOnly_SurfacesRejectionToTheModel()
        {
            client.Enqueue(new LlmResponse { ToolCalls = new[] { new LlmToolCall { Id = "c1", Name = "execute_query", ArgumentsJson = "{\"sql\":\"DELETE FROM Orders\"}" } } });
            client.Enqueue(new LlmResponse { Text = "That would change data; enable write-access first." });
            ChatAgent agent = CreateAgent();

            string answer = await agent.SendAsync("delete orders", CancellationToken.None);

            LlmRequest second = client.Requests[1];
            Assert.Contains(second.Messages, message => message.Role == LlmRole.Tool && message.Text!.Contains("read-only"));
            Assert.Empty(sqlService.ExecutedBatches);
            Assert.Equal("That would change data; enable write-access first.", answer);
        }

        [Fact]
        public async Task SendAsync_StopsAfterTooManyToolCalls()
        {
            for (int i = 0; i < ChatAgent.MaxIterations + 2; i++)
            {
                client.Enqueue(new LlmResponse { ToolCalls = new[] { new LlmToolCall { Id = "c" + i, Name = "list_tables", ArgumentsJson = "{}" } } });
            }

            ChatAgent agent = CreateAgent();

            string answer = await agent.SendAsync("loop", CancellationToken.None);

            Assert.Contains("too many tool calls", answer);
            Assert.Equal(ChatAgent.MaxIterations, client.Requests.Count);
        }

        /// <summary>
        /// Creates an agent over the fakes, locked to the "Sales" database.
        /// </summary>
        /// <returns>The agent.</returns>
        private ChatAgent CreateAgent()
        {
            DatabaseToolExecutor executor = new(sqlService, new SqlStatementAnalyzer(), "Sales", () => accessMode, null);

            return new ChatAgent(client, executor, "system");
        }
    }
}
