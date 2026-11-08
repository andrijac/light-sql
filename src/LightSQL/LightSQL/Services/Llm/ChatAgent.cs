using System.Text.Json;
using System.Text.Json.Nodes;
using LightSQL.Services.Tools;

namespace LightSQL.Services.Llm
{
    /// <summary>
    /// Drives a chat conversation: sends the user's message and the tool catalog to the LLM,
    /// runs any tool calls the model makes through the shared <see cref="DatabaseToolExecutor"/>,
    /// feeds the results back, and repeats until the model answers in text (or an iteration
    /// cap is hit). Holds the conversation history. Tool calls are reported through the
    /// executor's own activity callback, so the UI sees them without extra wiring.
    /// </summary>
    public sealed class ChatAgent
    {
        /// <summary>
        /// The most tool-call rounds allowed for a single user message, to stop runaway loops.
        /// </summary>
        public const int MaxIterations = 10;

        private readonly DatabaseToolExecutor executor;

        /// <summary>
        /// The LLM backend; swappable so the chat can change provider or model mid-conversation
        /// (all providers share the OpenAI-compatible wire format, so the history stays valid).
        /// </summary>
        private ILlmChatClient client;
        private readonly string systemPrompt;
        private readonly List<LlmMessage> history = new();

        /// <summary>
        /// Creates the agent.
        /// </summary>
        /// <param name="client">The LLM backend.</param>
        /// <param name="executor">The shared tool executor (bound to one connection + database).</param>
        /// <param name="systemPrompt">The system prompt (engine, database, rules, metadata).</param>
        public ChatAgent(ILlmChatClient client, DatabaseToolExecutor executor, string systemPrompt)
        {
            this.client = client;
            this.executor = executor;
            this.systemPrompt = systemPrompt;
        }

        /// <summary>
        /// The conversation so far (user, assistant and tool messages), oldest first.
        /// </summary>
        public IReadOnlyList<LlmMessage> History
        {
            get
            {
                return history;
            }
        }

        /// <summary>
        /// Sends a user message, runs the tool-call loop and returns the assistant's final text.
        /// </summary>
        /// <param name="userMessage">The user's message.</param>
        /// <param name="cancellationToken">Cancels the turn (e.g. the user pressed Stop).</param>
        /// <returns>The assistant's final answer.</returns>
        public async Task<string> SendAsync(string userMessage, CancellationToken cancellationToken)
        {
            history.Add(new LlmMessage { Role = LlmRole.User, Text = userMessage });

            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                LlmRequest request = new()
                {
                    SystemPrompt = systemPrompt,
                    Messages = history.ToList(),
                    Tools = DatabaseToolCatalog.Tools
                };

                LlmResponse response = await client.SendAsync(request, cancellationToken);

                if (response.ToolCalls.Count == 0)
                {
                    string answer = response.Text ?? string.Empty;
                    history.Add(new LlmMessage { Role = LlmRole.Assistant, Text = answer });

                    return answer;
                }

                history.Add(new LlmMessage { Role = LlmRole.Assistant, Text = response.Text, ToolCalls = response.ToolCalls });

                foreach (LlmToolCall call in response.ToolCalls)
                {
                    ToolCallOutcome outcome = await executor.ExecuteAsync(call.Name, ParseArguments(call.ArgumentsJson), cancellationToken);

                    history.Add(new LlmMessage { Role = LlmRole.Tool, ToolCallId = call.Id, Text = outcome.Text });
                }
            }

            const string tooMany = "Stopped: the assistant made too many tool calls without finishing. Try narrowing the question.";
            history.Add(new LlmMessage { Role = LlmRole.Assistant, Text = tooMany });

            return tooMany;
        }

        /// <summary>
        /// Swaps the LLM backend (e.g. the user picked a different provider or model). The
        /// conversation history is kept, since every provider uses the same wire format.
        /// </summary>
        /// <param name="client">The new LLM backend.</param>
        public void UseClient(ILlmChatClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Parses a tool call's raw JSON arguments into an object, or null when it is not valid
        /// JSON (the executor then treats required arguments as missing).
        /// </summary>
        /// <param name="argumentsJson">The raw arguments string.</param>
        /// <returns>The arguments object, or null.</returns>
        private static JsonObject? ParseArguments(string argumentsJson)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson) == true)
            {
                return new JsonObject();
            }

            try
            {
                return JsonNode.Parse(argumentsJson) as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
