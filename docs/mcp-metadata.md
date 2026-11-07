# LightSQL — Database metadata for MCP

An MCP server ([mcp-server.md](mcp-server.md)) lets an AI client read the live catalog
(table and column names, types, nullability). It does **not** expose primary/foreign keys,
relationships, enum meanings, units, soft-delete/tenant filters or business semantics — so
on an unfamiliar schema the AI guesses joins and filters from naming conventions.

**Database metadata** closes that gap: you paste a JSON document describing a database, and
LightSQL appends it to the MCP server's **instructions** (the AI's "system prompt"), so the
AI has it while constructing queries.

## Where it is stored

- One document per **connection + database**, in `%APPDATA%\LightSQL\metadata.json`.
- It is **sent to the AI client and its model provider**. Do **not** include passwords,
  tokens or connection strings.

## Adding or editing metadata

1. **Database → Database Metadata…**.
2. Choose the **Connection** (the saved connections are offered) and the **Database**. When
   opened while connected, both are prefilled from the current connection and the selected
   database.
3. Paste the JSON into **Metadata (JSON)** and **Save**. The JSON is validated; an empty box
   removes the saved entry.
4. The metadata applies the **next time a server starts** for that connection and database.
   Restart a running server (its tab's **Stop Server** / **Start Server**) to pick up edits.

The JSON is free-form — any shape the AI can read is fine. A per-table structure with keys,
relationships and column meanings works well.

## Generating the JSON from source code

If you have the source of the application that owns the database (ORM entities, migrations,
enums and local docs), you can have an AI agent extract the metadata for you.

LightSQL ships the full prompt for this. Open **Tools → Metadata Extraction Prompt…**, click
**Copy**, then paste it to an AI agent (e.g. Claude Code) running **inside that repository**.
The prompt assumes a single database in context and needs no editing, so it works on any
repo. It asks the agent to read the migrations, ORM / data-access layer, enums and docs,
reconstruct each table's final schema, and emit a `database-knowledge.md` (and optional
`database-knowledge.json`) covering keys, relationships, enum value maps, units and the
row-scoping filters a query should apply.

Paste the agent's output into the Database Metadata dialog for the matching connection and
database. It reflects the source, which can drift from the live database, so it complements —
rather than replaces — what the MCP tools read from the catalog.
