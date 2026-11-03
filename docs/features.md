# LightSQL — Features & Usage

## Window layout

```
┌ Menu: File | Edit | Query | Database | Tools | Help ───────────────────────────┐
├ Toolbar: [Connect] [Database ▼] [Execute (F5)] [Cancel] [Save Query] ──────────┤
├ Object Explorer │ SQL editor (line numbers, highlighting)                      │
│  (lazy tree)    │ ═══════ splitter ═════════════════════════════════════════   │
│                 │ Results │ Messages                                           │
├ Footer: status | connection | database | rows | elapsed | Ln, Col ─────────────┤
```

## Connecting

**Database → Connect** (or the toolbar **Connect** button) opens the connection
dialog. Two modes:

- **Form:** server name, authentication (Windows or SQL Server), login/password,
  optional database, Encrypt / Trust server certificate.
- **Connection string:** paste a full .NET connection string. An optional password
  box injects a password if the string does not contain one.

Saved connections are listed on the left; **Use** loads one into the form,
**Delete** removes it. Tick **Save this connection** (and optionally **Save
password**) to persist it. **Test** validates the details without connecting the app.

## Active database

The toolbar **Database** dropdown lists the server's databases and selects the one
used for execution. The Object Explorer and "Select Top 1000" also set it.

## Writing and running queries

- The editor provides T-SQL syntax highlighting, line numbers, and standard
  editing (undo/redo/cut/copy/paste/select-all via the Edit menu).
- **F5** (or **Query → Execute**, or the toolbar) runs the **selected text** if any,
  otherwise the whole editor.
- Results appear in the **Results** tab (one grid per result set). Informational
  output, row counts and errors appear in the **Messages** tab.
- The footer shows the current line/column, rows returned or affected, and the
  elapsed time.
- **Cancel** aborts a running query.

## UPDATE / DELETE safety guard

Before executing, LightSQL checks for an `UPDATE` or `DELETE` with **no WHERE
clause**. If found, it asks for confirmation. The check ignores matches inside
strings, comments, bracketed identifiers and subqueries. Disable it in
**Tools → Options**.

## Saving and opening queries

- **File → Save Query** (Ctrl+S) asks for a name and writes `<name>.sql` to your
  personal queries folder (default `%USERPROFILE%\LightSQL\Queries`, configurable in
  Options).
- **File → Open Query** lists the saved queries; selecting one loads it.
- **File → Refresh Saved Queries** re-reads the folder.

## Object Explorer

A lazy tree: **server → Databases → _database_ → Tables / Views → _object_ →
columns**. Each level loads when expanded. Right-click a node for:

- **New Query** — set the node's database active and clear the editor.
- **Select Top 1000** — generate and run a `SELECT TOP 1000` for a table/view.
- **New Database…** — prompt for a name and run `CREATE DATABASE`.
- **Drop…** — drop a table, view or database (with confirmation).
- **Refresh** — reload the node's children.

Creating databases is also available via **Database → New Database…**.

## Options (Tools → Options)

- Warn before executing UPDATE/DELETE without a WHERE clause (on by default).
- Queries folder (with a Browse button).
- Show line numbers.
- Editor font size.
