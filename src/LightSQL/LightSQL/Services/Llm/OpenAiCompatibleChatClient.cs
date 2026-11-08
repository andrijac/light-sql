using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using LightSQL.Services.Tools;

namespace LightSQL.Services.Llm
{
    /// <summary>
    /// An <see cref="ILlmChatClient"/> for OpenAI-compatible Chat Completions endpoints
    /// (OpenAI, Azure OpenAI, local gateways, Replicate's OpenAI-compatible endpoint, ...).
    /// Sends the standard <c>tools</c> / <c>tool_calls</c> shape. The base URL is the endpoint
    /// root (for example "https://api.replicate.com/v1"); "/chat/completions" is appended.
    /// </summary>
    public sealed class OpenAiCompatibleChatClient : ILlmChatClient
    {
        private const int MaxErrorBodyLength = 500;

        private readonly HttpClient httpClient;
        private readonly string baseUrl;
        private readonly string apiKey;
        private readonly string model;
        private readonly int maxTokens;
        private readonly bool useMaxCompletionTokens;

        /// <summary>
        /// Creates the client.
        /// </summary>
        /// <param name="httpClient">The HTTP client to send with (injected so tests can fake it).</param>
        /// <param name="baseUrl">The endpoint root, e.g. "https://api.openai.com/v1".</param>
        /// <param name="apiKey">The bearer API key.</param>
        /// <param name="model">The model name (e.g. "openai/gpt-oss-120b", "anthropic/claude-sonnet-5.5").</param>
        /// <param name="maxTokens">The maximum tokens to generate per reply.</param>
        /// <param name="useMaxCompletionTokens">
        /// When true, the reply-length cap is sent as <c>max_completion_tokens</c> (required by
        /// OpenAI's newer models, which reject <c>max_tokens</c>); when false, as <c>max_tokens</c>
        /// (what the other OpenAI-compatible providers expect).
        /// </param>
        public OpenAiCompatibleChatClient(
            HttpClient httpClient,
            string baseUrl,
            string apiKey,
            string model,
            int maxTokens = 2048,
            bool useMaxCompletionTokens = false)
        {
            this.httpClient = httpClient;
            this.baseUrl = baseUrl;
            this.apiKey = apiKey;
            this.model = model;
            this.maxTokens = maxTokens;
            this.useMaxCompletionTokens = useMaxCompletionTokens;
        }

        /// <inheritdoc />
        public async Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken cancellationToken)
        {
            JsonObject payload = new()
            {
                ["model"] = model,
                [useMaxCompletionTokens == true ? "max_completion_tokens" : "max_tokens"] = maxTokens,
                ["messages"] = BuildMessages(request)
            };

            JsonArray tools = BuildTools(request.Tools);

            if (tools.Count > 0)
            {
                payload["tools"] = tools;
                payload["tool_choice"] = "auto";
            }

            using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            httpRequest.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

            using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
            string body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (httpResponse.IsSuccessStatusCode == false)
            {
                throw new InvalidOperationException($"The AI provider returned {(int)httpResponse.StatusCode}: {Truncate(body)}");
            }

            return ParseResponse(body);
        }

        /// <summary>
        /// Builds the messages array (system prompt first, then the conversation).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The messages array.</returns>
        private static JsonArray BuildMessages(LlmRequest request)
        {
            JsonArray messages = new()
            {
                new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt }
            };

            foreach (LlmMessage message in request.Messages)
            {
                messages.Add(MapMessage(message));
            }

            return messages;
        }

        /// <summary>
        /// Maps one message to the OpenAI message shape.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The JSON object.</returns>
        private static JsonObject MapMessage(LlmMessage message)
        {
            if (message.Role == LlmRole.User)
            {
                return new JsonObject { ["role"] = "user", ["content"] = message.Text ?? string.Empty };
            }

            if (message.Role == LlmRole.Tool)
            {
                return new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = message.ToolCallId ?? string.Empty,
                    ["content"] = message.Text ?? string.Empty
                };
            }

            JsonObject assistant = new() { ["role"] = "assistant" };

            if (message.ToolCalls.Count > 0)
            {
                JsonArray toolCalls = new();

                foreach (LlmToolCall call in message.ToolCalls)
                {
                    toolCalls.Add(new JsonObject
                    {
                        ["id"] = call.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = call.ArgumentsJson }
                    });
                }

                assistant["tool_calls"] = toolCalls;
                assistant["content"] = message.Text;
            }
            else
            {
                assistant["content"] = message.Text ?? string.Empty;
            }

            return assistant;
        }

        /// <summary>
        /// Builds the tools array from the catalog definitions.
        /// </summary>
        /// <param name="tools">The tool definitions.</param>
        /// <returns>The tools array (empty when there are none).</returns>
        private static JsonArray BuildTools(IReadOnlyList<ToolDefinition> tools)
        {
            JsonArray array = new();

            foreach (ToolDefinition tool in tools)
            {
                JsonObject properties = new();
                JsonArray required = new();

                foreach (ToolParameter parameter in tool.Parameters)
                {
                    properties[parameter.Name] = new JsonObject
                    {
                        ["type"] = parameter.Type,
                        ["description"] = parameter.Description
                    };

                    if (parameter.Required == true)
                    {
                        required.Add(parameter.Name);
                    }
                }

                JsonObject parametersSchema = new()
                {
                    ["type"] = "object",
                    ["properties"] = properties
                };

                if (required.Count > 0)
                {
                    parametersSchema["required"] = required;
                }

                array.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = parametersSchema
                    }
                });
            }

            return array;
        }

        /// <summary>
        /// Parses the model's reply into an <see cref="LlmResponse"/>.
        /// </summary>
        /// <param name="body">The response body.</param>
        /// <returns>The parsed reply.</returns>
        private static LlmResponse ParseResponse(string body)
        {
            JsonNode? root = JsonNode.Parse(body);
            JsonNode? message = root?["choices"]?[0]?["message"];

            if (message is null)
            {
                return new LlmResponse { Text = string.Empty };
            }

            List<LlmToolCall> toolCalls = new();

            if (message["tool_calls"] is JsonArray calls)
            {
                foreach (JsonNode? callNode in calls)
                {
                    if (callNode is null)
                    {
                        continue;
                    }

                    JsonNode? function = callNode["function"];

                    toolCalls.Add(new LlmToolCall
                    {
                        Id = callNode["id"]?.GetValue<string>() ?? string.Empty,
                        Name = function?["name"]?.GetValue<string>() ?? string.Empty,
                        ArgumentsJson = function?["arguments"]?.GetValue<string>() ?? "{}"
                    });
                }
            }

            string? text = message["content"]?.GetValue<string>();

            return new LlmResponse { Text = text, ToolCalls = toolCalls };
        }

        /// <summary>
        /// Trims an error body to a reasonable length for a message.
        /// </summary>
        /// <param name="body">The response body.</param>
        /// <returns>The trimmed body.</returns>
        private static string Truncate(string body)
        {
            string trimmed = body.Trim();

            return trimmed.Length <= MaxErrorBodyLength ? trimmed : trimmed.Substring(0, MaxErrorBodyLength) + "...";
        }
    }
}
