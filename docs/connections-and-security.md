# LightSQL — Connections & Security

## Where data is stored

| Data | Location |
|---|---|
| Application options | `%APPDATA%\LightSQL\options.json` |
| Saved connections | `%APPDATA%\LightSQL\connections.json` |
| Query scripts | Wherever you save them — ordinary `.sql` files chosen with **File → Save As** |

Query files are plain text: don't put passwords or connection strings with
credentials in them.

## Credential handling

> Connection details can include SQL Server logins. Treat passwords, connection
> strings and tokens as sensitive and share them with care.

LightSQL is designed so a **plaintext password is never written to disk**:

- **Windows / Integrated authentication is the default.** With it, no password is
  stored at all.
- **SQL Server authentication** stores a password only when the user ticks
  **Save password**. The value is encrypted with the **Windows Data Protection API
  (DPAPI)**, scoped to the current Windows user (`DataProtectionScope.CurrentUser`),
  plus an application-specific entropy value. Only `connections.json`'s
  `protectedPassword` field (Base64 ciphertext) is written — never the plaintext.
- A DPAPI-encrypted value can only be decrypted by the **same Windows user on the
  same machine**. Copying `connections.json` to another user or machine makes the
  saved passwords unreadable (the app simply treats them as absent).
- When a connection is defined by a **raw connection string**, any embedded
  `Password=`/`PWD=` is **stripped out** before the string is saved; if the user
  opted to save the password it is moved into the encrypted field instead.
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
It is **read-only by default** (data-changing statements are rejected and every
batch is rolled back) until the user allows read-write in its tab. Any program on
this computer can call it while it runs, and query results are sent to the AI
client's model provider. Use a login with the least permissions needed, and stop
servers you no longer use.

## Recommendations

- Prefer Windows authentication where possible.
- Leave **Encrypt** on. For local/dev servers with self-signed certificates, keep
  **Trust server certificate** on; for production, use a trusted certificate.
- Do not commit `connections.json` to source control.
