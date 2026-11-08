# LightSQL — Connections & Security

## Where data is stored

| Data | Location |
|---|---|
| Application options | `%APPDATA%\LightSQL\options.json` |
| Saved connections | `%APPDATA%\LightSQL\connections.json` |
| Database metadata (for MCP + chat) | `%APPDATA%\LightSQL\metadata.json` |
| AI provider settings (for chat) | `%APPDATA%\LightSQL\llm.json` (one API key per provider, each DPAPI-encrypted) |
| Query scripts | Wherever you save them — ordinary `.sql` files chosen with **File → Save As** |

Query files are plain text: don't put passwords or connection strings with
credentials in them.

## Choosing a database engine

The connection dialog's **Database type** picker selects the engine: **Microsoft SQL
Server**, **PostgreSQL** or **MySQL / MariaDB** (see
[database-providers.md](database-providers.md)). The form adapts to it:

- **Authentication** (Windows vs SQL Server) is shown only for SQL Server. PostgreSQL
  and MySQL always use a username and password.
- **Port** is optional; blank uses the engine default (1433 / 5432 / 3306). For SQL
  Server the port is appended to the server as `server,port`.
- A **raw .NET connection string** is parsed with the selected engine's driver, so
  pick the engine first.

The engine is saved with each connection; profiles saved before multi-engine support
load as SQL Server.

## Credential handling

> Connection details include database logins. Treat passwords, connection strings
> and tokens as sensitive and share them with care.

LightSQL is designed so a **plaintext password is never written to disk**:

- **Windows / Integrated authentication is the default for SQL Server.** With it, no
  password is stored at all. (PostgreSQL and MySQL always use username/password.)
- **Username / password authentication** stores a password only when the user ticks
  **Save password**. The value is encrypted with the **Windows Data Protection API
  (DPAPI)**, scoped to the current Windows user (`DataProtectionScope.CurrentUser`),
  plus an application-specific entropy value. Only `connections.json`'s
  `protectedPassword` field (Base64 ciphertext) is written — never the plaintext.
- A DPAPI-encrypted value can only be decrypted by the **same Windows user on the
  same machine**. Copying `connections.json` to another user or machine makes the
  saved passwords unreadable (the app simply treats them as absent).
- When a connection is defined by a **raw connection string**, any embedded password
  is **stripped out** before the string is saved (using the selected engine's driver
  to parse it); if the user opted to save the password it is moved into the encrypted
  field instead.
- In the UI the password lives only in the `PasswordBox`; it is read in code-behind
  at connect/test time and never placed into a bindable property.

## Implementation references

- `DpapiCredentialProtector` — `Protect` / `Unprotect`.
- `ConnectionStore.Persist` — encrypts, strips raw-string passwords, writes JSON.
- `ConnectionProfile.Password` is marked `[JsonIgnore]`; only `ProtectedPassword`
  is serialized.

## MCP servers

An MCP server started from LightSQL ([mcp-server.md](mcp-server.md)) signs in
with the connection's credentials (kept in memory only, never shown or returned)
and listens on 127.0.0.1 only; requests from other sites' web pages are rejected.
Each server is **locked to a single database** and cannot reach any other database on
the connection. It is **read-only by default** (data-changing statements are rejected
and every batch is rolled back) until the user grants write-access in its tab. Any program
on this computer can call it while it runs, and query results are sent to the AI client's
model provider. Use a login with the least permissions needed, and stop servers you no
longer use.

**Database metadata** pasted via **Database → Database Metadata…** is saved in
`metadata.json` and sent to the AI client as part of the server's instructions, so it must
not contain passwords, tokens or connection strings. The file holds only what you paste;
LightSQL never writes credentials into it.

## In-app chat

The in-app chat ([chat.md](chat.md)) runs the same database tools in-process, locked to one
database and read-only by default. You pick an AI provider (Groq, Together, OpenRouter, OpenAI or
a local server) and model in the chat tab. Each provider's **API key** is stored encrypted (DPAPI,
like connection passwords) in `llm.json` — never in plaintext — and is sent only to its own
provider. Your chat messages and query results are sent to the selected provider, so keep the chat
read-only and don't expose data you may not share.

## Recommendations

- Prefer Windows authentication where possible (SQL Server).
- Leave **Encrypt** on. For local/dev servers with self-signed certificates, keep
  **Trust server certificate** on; for production, use a trusted certificate. The two
  checkboxes map onto each engine's SSL mode (see the table in
  [database-providers.md](database-providers.md)).
- Do not commit `connections.json` to source control.
