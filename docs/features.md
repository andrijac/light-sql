# LightSQL — Features & Usage

## Window layout

```
┌ Menu: File | Edit | Query | Database | Tools | Help ───────────────────────────┐
├ Toolbar: [Connect] [Database ▼] [Execute (F5)] [Cancel] [Save Query] ──────────┤
├ Object Explorer │ [SQLQuery1 ✕] [report *✕] [SQLQuery3 ✕]   ← query tabs     │
│  (lazy tree)    │ SQL editor of the selected tab (line numbers, highlighting)  │
│                 │ ═══════ splitter ═════════════════════════════════════════   │
│                 │ Results │ Messages   (of the selected tab)                   │
├ Footer: status | connection | tab database | tab status | rows | elapsed | Ln, Col ┤
```

The window title shows the selected tab (e.g. `report * - LightSQL`).

## Query tabs (SSMS-style)

Several queries can be open at once, each in its own tab, like SSMS query windows.
Every tab has its **own** editor (text, undo history, caret, selection), **own
database**, **own Results/Messages**, and its own execution and cancellation.

- **New tab:** **File → New Query** (Ctrl+N). New tabs are named `SQLQuery1`,
  `SQLQuery2`, … (numbers are not reused) and start on the selected tab's database.
- **Switch tabs:** click a header. The editor of the selected tab gets focus.
- **Close a tab:** the **✕** on the header, a **middle-click** on the header,
  **File → Close Query** (Ctrl+F4), or right-click → **Close**. Right-click →
  **Close All But This** closes the others.
- **Unsaved changes:** a tab whose text changed since it was opened/saved shows
  ` *` after its name. Closing it asks **Save / Don't Save / Cancel**
  (Yes / No / Cancel). Closing the application asks the same for every modified tab;
  **Cancel** keeps the application open.
- **Running queries:** a tab that is executing shows *(executing)* in its header.
  You can switch to another tab and execute there at the same time — **F5** and
  **Cancel** always act on the selected tab. Closing an executing tab cancels its
  query.
- If every tab is closed, a hint is shown; press Ctrl+N to start a new query.

## Connecting

**Database → Connect** (or the toolbar **Connect** button) opens the connection
dialog. Two modes:

- **Form:** server name, authentication (Windows or SQL Server), login/password,
  optional database, Encrypt / Trust server certificate.
- **Connection string:** paste a full .NET connection string. An optional password
  box injects a password if the string does not contain one.

Saved connections are listed on the left. **Selecting** one loads its details
(including a saved password) into the form, so you can press **Connect** straight
away; **double-click** it to connect in one step. **Delete** removes the selected
connection. Tick **Save this connection** (and optionally **Save password**) to
persist it. **Test** validates the details without connecting the app.

Saved passwords are encrypted with Windows DPAPI and never written in plain text.
For a connection-string connection, a password typed in the optional password box
or embedded in the string itself (`Password=...`) is removed from the saved string;
with **Save password** ticked it is stored encrypted and put back when connecting,
otherwise you must enter it again next time.

## Active database

The toolbar **Database** dropdown lists the server's databases and selects the one
used for execution **by the selected tab** — each tab remembers its own database.
Tabs opened from the Object Explorer use the node's database. On connect, tabs
without a database get the server's default; refreshing the list keeps each tab's
database (falling back to the default if it no longer exists). Disconnecting keeps
the tabs open but clears their database.

## Writing and running queries

- The editor provides T-SQL syntax highlighting, line numbers, and standard
  editing (undo/redo/cut/copy/paste/select-all via the Edit menu).
- **F5** (or **Query → Execute**, or the toolbar) runs the **selected text** if any,
  otherwise the whole editor.
- Results appear in the **Results** tab (one grid per result set). Informational
  output, row counts and errors appear in the **Messages** tab.
- The result grids **stretch to fill the results pane** and follow the window size
  (including maximize): one result set takes the whole pane, several share it
  equally. Each grid is at least 100 px tall; when more result sets are returned
  than fit, the pane scrolls. Each grid scrolls its own rows.
- The footer shows the selected tab's execution status, line/column, rows returned
  or affected, and the elapsed time.
- **Cancel** aborts the selected tab's running query.

## UPDATE / DELETE safety guard

Before executing, LightSQL checks for an `UPDATE` or `DELETE` with **no WHERE
clause**. If found, it asks for confirmation. The check ignores matches inside
strings, comments, bracketed identifiers and subqueries. Disable it in
**Tools → Options**.

## Saving and opening queries

- **File → Save Query** (Ctrl+S) asks for a name and writes the selected tab's text
  to `<name>.sql` in your personal queries folder (default
  `%USERPROFILE%\LightSQL\Queries`, configurable in Options). The tab is renamed to
  the saved name.
- **File → Open Query** lists the saved queries; selecting one opens it in a tab. If
  it is already open, its tab is activated; if the selected tab is an untouched new
  query, that tab is reused; otherwise a new tab is opened.
- **File → Refresh Saved Queries** re-reads the folder.

## Object Explorer

A lazy tree: **server → Databases → _database_ → Tables / Views → _object_ →
columns**. Each level loads when expanded.

Each node shows an icon for its type:

| Node | Icon |
|---|---|
| Server | grey server tower with a green light |
| Databases / Tables / Views folders | yellow folder — open while expanded, closed while collapsed |
| Database | blue cylinder |
| Table | grid with a blue header row |
| View | grid with a green header row and a lens |
| Column | single table column |
| Loading… (while a level loads) | grey hourglass, grey italic text |
| Load error | red circle with a cross, red text |

Right-click a node for:

- **New Query** — open a new, empty query tab on the node's database.
- **Select Top 1000** — open a new tab with a `SELECT TOP 1000` for a table/view
  and run it.
- **New Database…** — prompt for a name and run `CREATE DATABASE`.
- **Drop…** — drop a table, view or database (with confirmation).
- **Refresh** — reload the node's children.

Creating databases is also available via **Database → New Database…**.

## Options (Tools → Options)

- Warn before executing UPDATE/DELETE without a WHERE clause (on by default).
- Queries folder (with a Browse button).
- Show line numbers.
- Editor font size.

Editor options apply to every open tab immediately when saved.
