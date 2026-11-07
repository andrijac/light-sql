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

If you have the source of an application that owns the database (for example EF Core or
Dapper code with **FluentMigrator** migrations, plus local docs), you can have an AI agent
extract a metadata document. Point an agent (e.g. Claude Code) at that repository and use a
prompt such as:

```text
You are a database documentation specialist with read access to the source of an
application that owns a relational database: {{API_REPO_PATH}}.

Produce a single JSON "database knowledge pack" that an AI will use to write correct SQL
against this database through an MCP server. The AI can already read the live catalog
(table/column names, types, nullability); your document must add what the catalog does not
expose — primary/foreign keys, relationships, enum value meanings, units, computed columns,
and the row-scoping filters a query should apply.

Sources (most authoritative first for each fact):
1. FluentMigrator migrations — the physical schema. IMPORTANT: migrations are cumulative;
   read them in order and reconstruct each table's FINAL state (Create/Alter/Rename/Delete
   of tables and columns, Create.PrimaryKey, Create.ForeignKey, Create.Index, unique
   constraints, defaults, identity, nullability). Do not stop at the first Create.Table.
2. ORM / data-access layer (entities + Fluent config, repositories) — how columns are
   interpreted: property↔column mappings, navigation properties (relationships, incl. ones
   NOT enforced by a DB FK), value converters, global query filters (e.g. soft delete).
3. Enums and constant/lookup classes — CRITICAL. For every enum, record the full
   value → name map and which column(s) store it.
4. Local .md docs — domain glossary, business rules, intent.
5. Query/service code — canonical join paths and filters that appear everywhere.

Output one JSON document with:
- global: naming conventions; cross-cutting rules (soft-delete column and its "active"
  value, tenant/row-scoping columns, audit columns, UTC dates, money units); an enum
  dictionary (each enum, underlying type, value map, columns that use it); a domain glossary;
  a relationship/join map.
- tables[]: for each table — purpose; columns[] (name, type, nullable, default, meaning,
  units, enum reference, FK target, computed/deprecated); primaryKey; foreignKeys[]
  (outgoing and incoming, marking DB-enforced vs code-only); indexes[]; the filters a query
  should almost always apply; and table-specific query tips.

Rules: cite the source (file + migration/class) for non-obvious facts; mark inferred vs
explicit and flag conflicts; never invent columns, values or relationships; prefer
migrations for structure, code/enums for meaning, docs for intent. Optimize every entry for
"does this help an agent write a correct, well-filtered query?"
```

Paste the resulting JSON into the Database Metadata dialog for the matching connection and
database. The JSON reflects the source, which can drift from the live database, so it
complements — rather than replaces — what the MCP tools read from the catalog.
