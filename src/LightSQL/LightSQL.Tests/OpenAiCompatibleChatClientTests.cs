using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using LightSQL.Services.Llm;
using LightSQL.Services.Tools;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the OpenAI-compatible chat client's wire format: the request it builds (model,
    /// system message, tools, auth header) and parsing both tool-call and plain-text replies.
    /// </summary>
    public class OpenAiCompatibleChatClientTests
    {
        [Fact]
        public async Task SendAsync_BuildsRequest_AndParsesToolCall()
        {
            const string responseJson = "{\"choices\":[{\"message\":{\"content\":null,\"tool_calls\":"
                + "[{\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"list_tables\",\"arguments\":\"{}\"}}]}}]}";
            RecordingHandler handler = new(responseJson);
            OpenAiCompatibleChatClient client = new(new HttpClient(handler), "https://api.example.com/v1", "key", "test-model");

            LlmResponse response = await client.SendAsync(new LlmRequest
            {
                SystemPrompt = "sys",
                Messages = new[] { new LlmMessage { Role = LlmRole.User, Text = "hi" } },
                Tools = DatabaseToolCatalog.Tools
            }, CancellationToken.None);

            LlmToolCall call = Assert.Single(response.ToolCalls);
            Assert.Equal("list_tables", call.Name);
            Assert.Equal("call_1", call.Id);

            JsonNode sent = JsonNode.Parse(handler.RequestBody!)!;
            Assert.Equal("test-model", sent["model"]!.GetValue<string>());
            Assert.Equal("system", sent["messages"]![0]!["role"]!.GetValue<string>());
            Assert.Equal("auto", sent["tool_choice"]!.GetValue<string>());
            Assert.Equal("Bearer key", handler.AuthHeader);

            JsonArray tools = sent["tools"]!.AsArray();
            Assert.Contains(tools, tool => tool!["function"]!["name"]!.GetValue<string>() == "execute_query");
        }

        [Fact]
        public async Task SendAsync_ByDefault_SendsMaxTokens()
        {
            RecordingHandler handler = new("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}");
            OpenAiCompatibleChatClient client = new(new HttpClient(handler), "https://x/v1", "k", "m", 321);

            await client.SendAsync(new LlmRequest { SystemPrompt = "s", Messages = Array.Empty<LlmMessage>() }, CancellationToken.None);

            JsonNode sent = JsonNode.Parse(handler.RequestBody!)!;
            Assert.Equal(321, sent["max_tokens"]!.GetValue<int>());
            Assert.Null(sent["max_completion_tokens"]);
        }

        [Fact]
        public async Task SendAsync_WhenUsingMaxCompletionTokens_SendsThatInsteadOfMaxTokens()
        {
            RecordingHandler handler = new("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}");
            OpenAiCompatibleChatClient client = new(new HttpClient(handler), "https://api.openai.com/v1", "k", "gpt-5.5", 321, useMaxCompletionTokens: true);

            await client.SendAsync(new LlmRequest { SystemPrompt = "s", Messages = Array.Empty<LlmMessage>() }, CancellationToken.None);

            JsonNode sent = JsonNode.Parse(handler.RequestBody!)!;
            Assert.Equal(321, sent["max_completion_tokens"]!.GetValue<int>());
            Assert.Null(sent["max_tokens"]);
        }

        [Fact]
        public async Task SendAsync_ParsesPlainText()
        {
            const string responseJson = "{\"choices\":[{\"message\":{\"content\":\"Hello there\"}}]}";
            OpenAiCompatibleChatClient client = new(new HttpClient(new RecordingHandler(responseJson)), "https://x/v1", "k", "m");

            LlmResponse response = await client.SendAsync(
                new LlmRequest { SystemPrompt = "s", Messages = Array.Empty<LlmMessage>() },
                CancellationToken.None);

            Assert.Equal("Hello there", response.Text);
            Assert.Empty(response.ToolCalls);
        }

        [Fact]
        public async Task SendAsync_Throws_OnHttpError()
        {
            OpenAiCompatibleChatClient client = new(
                new HttpClient(new RecordingHandler("nope", HttpStatusCode.Unauthorized)),
                "https://x/v1",
                "k",
                "m");

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.SendAsync(new LlmRequest { SystemPrompt = "s", Messages = Array.Empty<LlmMessage>() }, CancellationToken.None));

            Assert.Contains("401", exception.Message);
        }

        /// <summary>
        /// Captures the outgoing request and returns a canned response.
        /// </summary>
        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly string responseBody;
            private readonly HttpStatusCode statusCode;

            public RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
            {
                this.responseBody = responseBody;
                this.statusCode = statusCode;
            }

            public string? RequestBody { get; private set; }

            public string? AuthHeader { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                AuthHeader = request.Headers.TryGetValues("Authorization", out IEnumerable<string>? values) == true
                    ? string.Join(string.Empty, values)
                    : null;

                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
                };
            }
        }
    }
}
