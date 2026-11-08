# LightSQL — In-app Chat (DB assistant)

The **Chat** tab lets you ask an LLM about the connected database from inside LightSQL. The
model runs the same database tools as the MCP server — list tables, describe a table, run SQL —
through the shared tool core, so it answers questions, explores the schema and writes queries
for you. It is **bound to a single database** and **read-only by default**.

Unlike the [MCP server](mcp-server.md) (which external AI clients connect to over HTTP), the
chat calls the tools **in-process** — there is no network hop and no separate client to set up.

## Add an API key (once)

The chat needs an AI provider. Open **Tools → AI Provider…** and paste an **API key** for each
provider you want to use. The supported providers are **preconfigured** (endpoint and a list of
suggested models), so you only supply the key:

| Provider | Endpoint | Get a key |
|---|---|---|
| **Groq** | `https://api.groq.com/openai/v1` | <https://console.groq.com/keys> |
| **Together AI** | `https://api.together.xyz/v1` | <https://api.together.ai/settings/api-keys> |
| **OpenRouter** | `https://openrouter.ai/api/v1` | <https://openrouter.ai/keys> |
| **OpenAI** | `https://api.openai.com/v1` | <https://platform.openai.com/api-keys> |
| **Local (Ollama / LM Studio)** | `http://localhost:11434/v1` | *no key needed* |

- Each key is stored **encrypted** (Windows DPAPI) in `%APPDATA%\LightSQL\llm.json`, is sent only
  to its own provider, and is never shown again (leave a provider's box blank to keep its key).
- **Max tokens** caps the reply length (shared by all providers).

> All providers are used through their **OpenAI-compatible** Chat Completions endpoint with
> function calling, so the chat can run tools on any of them. The suggested model lists are
> starting points — the model box in the chat is editable, so you can type any model the provider
> offers. For a **local** provider, tool calling depends on the model you run.

## Start a chat

Connect first, then start a chat **for a single database** (like the MCP server):

- The **Chat** toolbar button, next to the database dropdown — uses the selected database.
- **Database → Chat with Database**.
- Right-click a **database node** in the Object Explorer → **Chat with Database** (the item
  appears only on a database node).

Each chat opens in its own tab (`Chat <database>`). At the top of the tab, pick the **provider**
and **model** (both default to the ones you used last). Type a question and **Send**; **Stop**
cancels an in-flight reply. You can switch provider or model mid-chat; the conversation is kept.

The transcript shows your messages and the assistant's replies. The **tool calls** the model
makes in a turn are grouped into a **collapsed panel** (click it to expand) whose title is the
current tool, so you can see activity at a glance without the tool noise.

### Copying

- **Copy conversation** (button above the transcript) copies the whole conversation — your
  messages and the assistant's replies — to the clipboard. Tool calls are left out.
- **Copy** on any message copies just that message or reply.

## Safety

- **Single-database lock.** The chat can only touch its one database — a tool call naming
  another database is rejected, exactly as for the MCP server.
- **Read-only by default.** `execute_query` rejects data-changing statements and rolls every
  batch back until you tick **Allow write-access** (which asks for confirmation). In
  write-access mode the model can change data with the connection login's permissions.
- **Least-privilege login** is still your strongest guarantee.
- **Your messages and query results are sent to the configured provider.** Don't expose data
  you may not share, and keep the chat read-only unless you need writes.

## Database metadata

The chat reuses the same metadata you paste via **Database → Database Metadata…** (see
[mcp-metadata.md](mcp-metadata.md)): when a chat starts, the document for that connection +
database (JSON, Markdown or any text) is folded into the model's system prompt, so it has the
keys, relationships, enum value maps and filters the live catalog doesn't expose. This matters
most here, because the chat may run a smaller, cheaper model. (Edit the metadata, then open a
new chat to pick it up.)

## Implementation

| Type | Role |
|---|---|
| `Services/Tools/DatabaseToolExecutor` | Transport-neutral tool core (list databases/tables, describe, execute) with the single-database lock, read-only guard and result formatting. Shared by the MCP server and the chat. |
| `Services/Tools/DatabaseToolCatalog` | The tool definitions (name, description, JSON-schema parameters) used to build the LLM's function-calling schema; kept in sync with the MCP tools by a test. |
| `Services/Tools/DatabaseAssistantPrompt` | Builds the system prompt (engine, locked database, rules) and appends the pasted metadata. Shared by the MCP server and the chat. |
| `Services/Llm/ILlmChatClient`, `OpenAiCompatibleChatClient` | The LLM backend abstraction and the OpenAI-compatible implementation (Chat Completions `tools`/`tool_calls`), used by every provider. |
| `Services/Llm/LlmProviderCatalog`, `Models/LlmProviderDefinition` | The fixed set of providers (endpoint, whether a key is needed, suggested models). |
| `Services/Llm/ChatAgent` | The agent loop: send → run tool calls via the executor → feed results back → repeat (capped) → final answer. Its client is swappable so the user can change provider/model mid-chat. |
| `Services/Llm/ILlmClientFactory`, `LlmClientFactory` | Builds a client for a chosen provider + model from the saved per-provider key, and remembers the last selection. |
| `Services/ILlmSettingsStore`, `LlmSettingsStore`, `Models/LlmSettings`, `Models/LlmProviderCredential` | Settings in `llm.json`: one key per provider, each DPAPI-encrypted (`ProtectedApiKey`), never written in plaintext, plus the last-used provider/model. |
| `ViewModels/ChatTabViewModel`, `Views/ChatTabView` | The chat tab: provider + model pickers, transcript, input, Send/Stop, the write-access toggle, collapsible tool-call panels, and copy (whole conversation / per message). |
| `Models/ChatTranscriptItem`, `ChatMessage`, `ChatToolGroup`, `ChatToolCall` | The transcript items: a base type holding either a message line (user/assistant/error) or a collapsible group of tool calls (collapsed, titled by the current tool). |
| `ViewModels/LlmSettingsDialogViewModel`, `ViewModels/LlmProviderKeyRow`, `Views/LlmSettingsDialog` | The AI Provider dialog (one key field per provider, each read from its `PasswordBox` at save time). |

Tests: `DatabaseToolExecutorTests`, `DatabaseToolCatalogTests`, `ChatAgentTests`,
`OpenAiCompatibleChatClientTests`, `LlmProviderCatalogTests`, `LlmSettingsStoreTests`,
`LlmClientFactoryTests`, `LlmSettingsDialogViewModelTests`, `ChatTabViewModelTests`.

## Possible next steps

- More providers (the catalog is a single list to extend).
- An **Anthropic (Claude)** backend (its Messages API needs its own `ILlmChatClient`, since it is
  not OpenAI-compatible) behind the same provider picker.
- Streaming replies (token-by-token) and auto-scroll.
