# LightSQL — Features & Usage

## Running the app

Double-click `run.cmd` in the repository root, or run it from a terminal:

```bash
run.cmd
```

It builds the app (Debug) and starts it, then returns without waiting for the
window to close. `.sql` files given as arguments open in query tabs
(`run.cmd scripts\report.sql`); relative paths are relative to the folder you run
it from. It needs the .NET 10 SDK. If LightSQL is already running, it says so and
stops, because the running app locks its `.exe` and the build could not replace it.

## Window layout

```
┌ Menu: File | Edit | Query | Database | Tools | Help ───────────────────────────┐
├ Toolbar: [Connect] [Database ▼] [Execute (F5)] [Cancel] [Open] [Save] ────────┤
├ Object Explorer │ [SQLQuery1 ✕] [report.sql *✕] [SQLQuery3 ✕]   ← query tabs │
│  (lazy tree)    │ SQL editor of the selected tab (line numbers, highlighting)  │
│                 │ ═══════ splitter ═════════════════════════════════════════   │
│                 │ Results │ Messages   (of the selected tab)                   │
├ Footer: status | connection | tab database | tab status | rows | elapsed | Ln, Col ┤
```

The window title shows the selected tab (e.g. `report.sql * - LightSQL`).

## Query tabs (SSMS-style)

Several queries can be open at once, each in its own tab, like SSMS query windows.
Every tab has its **own** editor (text, undo history, caret, selection), **own
database**, **own Results/Messages**, and its own execution and cancellation.

- **New tab:** **File → New Query** (Ctrl+N). New tabs are named `SQLQuery1`,
  `SQLQuery2`, … (numbers are not reused) and start on the selected tab's database.
  A tab opened from a file is named after the file (e.g. `report.sql`); hover the
  tab to see the full path.
- **Switch tabs:** click a header. The editor of the selected tab gets focus.
- **Close a tab:** the **✕** on the header, a **middle-click** on the header,
  **File → Close** (Ctrl+F4), or right-click → **Close**. Right-click →
  **Close All But This** closes the others.
- **Unsaved changes:** a tab whose text changed since it was opened/saved shows
  ` *` after its name. Closing it asks **Save / Don't Save / Cancel**
  (Yes / No / Cancel); **Save** on a tab that has no file yet shows **Save As**, and
  cancelling that keeps the tab open. Closing the application asks the same for
  every modified tab; **Cancel** keeps the application open.
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
used for execution **by the selected tab** — each tab remembers its own database,
and switching tabs re-selects that tab's database in the dropdown. Tabs opened from
the Object Explorer use the node's database. On connect, tabs without a database
get the server's default; refreshing the list keeps each tab's database (falling
back to the default if it no longer exists). Disconnecting keeps the tabs open but
clears their database.

- **`USE` follows the query, like SSMS:** executing a batch that contains
  `USE Sales` switches that tab (and the dropdown) to `Sales`, and later executions
  in the tab run there. This also happens when a statement after the `USE` fails,
  because the switch already took place on the server. Other tabs are not affected.
- A tab's database that is missing from the dropdown list (e.g. created by a query
  after the list was loaded) is added to the list, in name order, so the dropdown
  can always show it. Names are matched case-insensitively and shown with the
  server's spelling.

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

## Opening and saving files

Queries are ordinary `.sql` files that you open and save anywhere, like in SSMS or
any text editor.

| Command | Shortcut | What it does |
|---|---|---|
| **File → New Query** | Ctrl+N | Opens a new, empty tab (no file yet). |
| **File → Open File...** | Ctrl+O | Shows the Windows **Open** dialog (`*.sql`, `*.txt`, all files). Select one or several files; each opens in a tab. |
| **File → Save** | Ctrl+S | Saves the selected tab to its file. A tab with no file yet shows **Save As** instead. |
| **File → Save As...** | Ctrl+Shift+S | Saves the selected tab to a new file chosen in the Windows **Save As** dialog (which asks before overwriting). The tab then refers to the new file; the old file is left as it was. |
| **File → Close** | Ctrl+F4 | Closes the selected tab (asks to save unsaved changes). |

- Right-clicking a tab offers **Save** and **Save As...** for that tab.
- The toolbar has **Open** and **Save** buttons.
- **Drag and drop:** drop `.sql` files from Explorer anywhere on the window to open
  them.
- **Command line:** `LightSQL.exe script1.sql script2.sql` opens the files at
  startup, so LightSQL can be used with **Open with** for `.sql` files.
- Opening a file that is already open activates its tab. If the selected tab is an
  untouched new query, the first file opens in it; otherwise each file gets a new
  tab, using the selected tab's database.
- A file that cannot be read or written (missing, locked, access denied) is reported
  in an error message; nothing else changes and the tab keeps its unsaved changes.
- The Open / Save As dialogs start in the folder of the selected tab's file, or else
  the folder of the last file opened or saved.
- **Encoding:** a file is saved back in the encoding it was opened with (UTF-8 with
  or without a byte order mark, UTF-16, UTF-32, or the Windows ANSI code page for
  older non-Unicode scripts). New files are saved as UTF-8 with a byte order mark,
  the same as SSMS.

Queries saved by earlier versions in `%USERPROFILE%\LightSQL\Queries` are ordinary
`.sql` files: open them with **File → Open File...**.

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

Right-clicking a node first selects it (the highlight moves to it, as in SSMS), so
it is always clear which node the context menu acts on. Right-click a node for:

- **New Query** — open a new, empty query tab on the node's database.
- **Select Top 1000** — open a new tab with a `SELECT TOP 1000` for a table/view
  and run it.
- **New Database…** — prompt for a name and run `CREATE DATABASE`.
- **Drop…** — drop a table, view or database (with confirmation).
- **Refresh** — reload the node's children.

Creating databases is also available via **Database → New Database…**.

## Options (Tools → Options)

- Warn before executing UPDATE/DELETE without a WHERE clause (on by default).
- Show line numbers.
- Editor font size.

Editor options apply to every open tab immediately when saved.
