using LightSQL.Services.Llm;

namespace LightSQL.Tests
{
    /// <summary>
    /// An <see cref="ILlmChatClient"/> that returns scripted responses in order and records the
    /// requests it was sent, for agent-loop tests.
    /// </summary>
    public sealed class FakeLlmChatClient : ILlmChatClient
    {
        private readonly Queue<LlmResponse> responses = new();

        /// <summary>
        /// The requests received, in order.
        /// </summary>
        public List<LlmRequest> Requests { get; } = new();

        /// <summary>
        /// Queues the next response to return.
        /// </summary>
        /// <param name="response">The response.</param>
        public void Enqueue(LlmResponse response)
        {
            responses.Enqueue(response);
        }

        /// <inheritdoc />
        public Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            LlmResponse response = responses.Count > 0
                ? responses.Dequeue()
                : new LlmResponse { Text = "(no scripted response)" };

            return Task.FromResult(response);
        }
    }
}
