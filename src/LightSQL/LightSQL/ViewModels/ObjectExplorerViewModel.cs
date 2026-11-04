using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// Builds and manages the lazy Object Explorer tree (server, databases, tables,
    /// views and columns) and the context-menu actions on those nodes.
    /// </summary>
    public partial class ObjectExplorerViewModel : ObservableObject
    {
        private const int SelectTopRowCount = 1000;

        private readonly ISqlConnectionService sqlService;
        private readonly IDialogService dialogService;

        [ObservableProperty]
        private TreeNodeViewModel? selectedNode;

        /// <summary>
        /// Creates the Object Explorer view model.
        /// </summary>
        /// <param name="sqlService">Service used to read server metadata.</param>
        /// <param name="dialogService">Service for prompts and confirmations.</param>
        public ObjectExplorerViewModel(ISqlConnectionService sqlService, IDialogService dialogService)
        {
            this.sqlService = sqlService;
            this.dialogService = dialogService;
        }

        /// <summary>
        /// The root nodes of the tree (a single server node once connected).
        /// </summary>
        public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = new();

        /// <summary>
        /// Callback surface for actions that belong to the main window. Set by
        /// <see cref="MainViewModel"/> after construction.
        /// </summary>
        public IMainActions? MainActions { get; set; }

        /// <summary>
        /// Rebuilds the tree for a freshly connected server and expands the root.
        /// </summary>
        /// <param name="serverName">The connected server name.</param>
        public void Build(string serverName)
        {
            RootNodes.Clear();

            TreeNodeViewModel server = new(serverName, DatabaseObjectType.Server, LoadServerChildrenAsync);
            RootNodes.Add(server);

            // Expanding the server triggers lazy loading of its "Databases" folder.
            server.IsExpanded = true;
        }

        /// <summary>
        /// Clears the tree (on disconnect).
        /// </summary>
        public void Clear()
        {
            RootNodes.Clear();
        }

        /// <summary>
        /// Refreshes the children of a node, or the whole tree when none is given.
        /// </summary>
        /// <param name="node">The node to refresh, or null for the root.</param>
        /// <returns>A task that completes when the refresh finishes.</returns>
        [RelayCommand]
        private async Task RefreshAsync(TreeNodeViewModel? node)
        {
            TreeNodeViewModel? target = node ?? (RootNodes.Count > 0 ? RootNodes[0] : null);

            if (target is not null)
            {
                await target.RefreshAsync();
            }
        }

        /// <summary>
        /// Prompts for a name and creates a new database.
        /// </summary>
        /// <param name="node">The invoking node (used to locate the databases folder).</param>
        /// <returns>A task that completes when creation finishes.</returns>
        [RelayCommand]
        private async Task NewDatabaseAsync(TreeNodeViewModel? node)
        {
            string? name = dialogService.PromptForText(
                "New Database",
                "Database name:",
                string.Empty,
                candidate => string.IsNullOrWhiteSpace(candidate) == false);

            if (name is null)
            {
                return;
            }

            try
            {
                await sqlService.CreateDatabaseAsync(name, CancellationToken.None);

                await RefreshDatabasesFolderAsync();

                if (MainActions is not null)
                {
                    await MainActions.RefreshDatabaseListAsync();
                }

                dialogService.ShowInformation($"Database '{name}' created.", "New Database");
            }
            catch (Exception ex)
            {
                dialogService.ShowError(ex.Message, "New Database");
            }
        }

        /// <summary>
        /// Opens a new, empty query tab against the node's database.
        /// </summary>
        /// <param name="node">The database node.</param>
        [RelayCommand]
        private void NewQuery(TreeNodeViewModel? node)
        {
            if (node is null || MainActions is null)
            {
                return;
            }

            string? database = string.IsNullOrWhiteSpace(node.DatabaseName) == true ? null : node.DatabaseName;

            MainActions.OpenQueryTab(database, string.Empty);
        }

        /// <summary>
        /// Builds a SELECT TOP query for a table or view node, opens it in a new query
        /// tab and runs it (like SSMS).
        /// </summary>
        /// <param name="node">The table or view node.</param>
        /// <returns>A task that completes when execution finishes.</returns>
        [RelayCommand]
        private async Task SelectTopAsync(TreeNodeViewModel? node)
        {
            if (node is null || MainActions is null)
            {
                return;
            }

            if (node.ObjectType != DatabaseObjectType.Table && node.ObjectType != DatabaseObjectType.View)
            {
                return;
            }

            string sql = $"SELECT TOP {SelectTopRowCount} * FROM {BracketQualified(node.SchemaName, node.ObjectName)}";

            MainActions.OpenQueryTab(node.DatabaseName, sql);

            await MainActions.ExecuteAsync();
        }

        /// <summary>
        /// Drops a database, table or view after confirmation.
        /// </summary>
        /// <param name="node">The node to drop.</param>
        /// <returns>A task that completes when the drop finishes.</returns>
        [RelayCommand]
        private async Task DropAsync(TreeNodeViewModel? node)
        {
            if (node is null)
            {
                return;
            }

            string? dropSql = BuildDropStatement(node);

            if (dropSql is null)
            {
                return;
            }

            bool confirmed = dialogService.Confirm(
                $"Are you sure you want to drop {node.Title}? This cannot be undone.",
                "Confirm Drop");

            if (confirmed == false)
            {
                return;
            }

            try
            {
                // Databases are dropped from master; tables/views from their own database.
                // The database is passed explicitly so no query tab's database changes.
                string targetDatabase = node.ObjectType == DatabaseObjectType.Database ? "master" : node.DatabaseName;

                QueryExecutionResult result = await sqlService.ExecuteBatchAsync(dropSql, targetDatabase, CancellationToken.None);

                if (result.IsSuccess == false)
                {
                    dialogService.ShowError(result.Messages, "Drop Failed");

                    return;
                }

                if (node.ObjectType == DatabaseObjectType.Database)
                {
                    await RefreshDatabasesFolderAsync();

                    if (MainActions is not null)
                    {
                        await MainActions.RefreshDatabaseListAsync();
                    }
                }
                else if (node.Parent is not null)
                {
                    await node.Parent.RefreshAsync();
                }
            }
            catch (Exception ex)
            {
                dialogService.ShowError(ex.Message, "Drop Failed");
            }
        }

        /// <summary>
        /// Loads the children of the server node: the "Databases" grouping folder.
        /// </summary>
        /// <returns>The server's child nodes.</returns>
        private Task<IReadOnlyList<TreeNodeViewModel>> LoadServerChildrenAsync()
        {
            TreeNodeViewModel databasesFolder = new("Databases", DatabaseObjectType.DatabasesFolder, LoadDatabasesAsync);

            IReadOnlyList<TreeNodeViewModel> children = new List<TreeNodeViewModel> { databasesFolder };

            return Task.FromResult(children);
        }

        /// <summary>
        /// Loads the list of databases on the server.
        /// </summary>
        /// <returns>One node per database.</returns>
        private async Task<IReadOnlyList<TreeNodeViewModel>> LoadDatabasesAsync()
        {
            IReadOnlyList<string> names = await sqlService.GetDatabasesAsync(CancellationToken.None);
            List<TreeNodeViewModel> nodes = new();

            foreach (string name in names)
            {
                TreeNodeViewModel node = new(name, DatabaseObjectType.Database, () => LoadDatabaseChildrenAsync(name))
                {
                    DatabaseName = name
                };

                nodes.Add(node);
            }

            return nodes;
        }

        /// <summary>
        /// Loads the children of a database node: the Tables and Views folders.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>The database's child folders.</returns>
        private Task<IReadOnlyList<TreeNodeViewModel>> LoadDatabaseChildrenAsync(string database)
        {
            TreeNodeViewModel tablesFolder = new("Tables", DatabaseObjectType.TablesFolder, () => LoadTablesAsync(database))
            {
                DatabaseName = database
            };

            TreeNodeViewModel viewsFolder = new("Views", DatabaseObjectType.ViewsFolder, () => LoadViewsAsync(database))
            {
                DatabaseName = database
            };

            IReadOnlyList<TreeNodeViewModel> children = new List<TreeNodeViewModel> { tablesFolder, viewsFolder };

            return Task.FromResult(children);
        }

        /// <summary>
        /// Loads the tables of a database.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>One node per table.</returns>
        private async Task<IReadOnlyList<TreeNodeViewModel>> LoadTablesAsync(string database)
        {
            IReadOnlyList<SchemaObject> tables = await sqlService.GetTablesAsync(database, CancellationToken.None);
            List<TreeNodeViewModel> nodes = new();

            foreach (SchemaObject table in tables)
            {
                TreeNodeViewModel node = new(table.QualifiedName, DatabaseObjectType.Table, () => LoadColumnsAsync(database, table.Schema, table.Name))
                {
                    DatabaseName = database,
                    SchemaName = table.Schema,
                    ObjectName = table.Name
                };

                nodes.Add(node);
            }

            return nodes;
        }

        /// <summary>
        /// Loads the views of a database.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>One node per view.</returns>
        private async Task<IReadOnlyList<TreeNodeViewModel>> LoadViewsAsync(string database)
        {
            IReadOnlyList<SchemaObject> views = await sqlService.GetViewsAsync(database, CancellationToken.None);
            List<TreeNodeViewModel> nodes = new();

            foreach (SchemaObject view in views)
            {
                TreeNodeViewModel node = new(view.QualifiedName, DatabaseObjectType.View, () => LoadColumnsAsync(database, view.Schema, view.Name))
                {
                    DatabaseName = database,
                    SchemaName = view.Schema,
                    ObjectName = view.Name
                };

                nodes.Add(node);
            }

            return nodes;
        }

        /// <summary>
        /// Loads the columns of a table or view.
        /// </summary>
        /// <param name="database">The owning database.</param>
        /// <param name="schema">The object schema.</param>
        /// <param name="objectName">The object name.</param>
        /// <returns>One leaf node per column.</returns>
        private async Task<IReadOnlyList<TreeNodeViewModel>> LoadColumnsAsync(string database, string schema, string objectName)
        {
            IReadOnlyList<ColumnInfo> columns = await sqlService.GetColumnsAsync(database, schema, objectName, CancellationToken.None);
            List<TreeNodeViewModel> nodes = new();

            foreach (ColumnInfo column in columns)
            {
                nodes.Add(new TreeNodeViewModel(column.DisplayText, DatabaseObjectType.Column));
            }

            return nodes;
        }

        /// <summary>
        /// Finds and refreshes the "Databases" folder under the server node.
        /// </summary>
        /// <returns>A task that completes when the refresh finishes.</returns>
        private async Task RefreshDatabasesFolderAsync()
        {
            if (RootNodes.Count == 0)
            {
                return;
            }

            TreeNodeViewModel server = RootNodes[0];

            foreach (TreeNodeViewModel child in server.Children)
            {
                if (child.ObjectType == DatabaseObjectType.DatabasesFolder)
                {
                    await child.RefreshAsync();

                    return;
                }
            }
        }

        /// <summary>
        /// Builds the DROP statement for a droppable node, or null when not droppable.
        /// </summary>
        /// <param name="node">The node to drop.</param>
        /// <returns>The DROP statement, or null.</returns>
        private static string? BuildDropStatement(TreeNodeViewModel node)
        {
            switch (node.ObjectType)
            {
                case DatabaseObjectType.Table:
                {
                    return $"DROP TABLE {BracketQualified(node.SchemaName, node.ObjectName)}";
                }

                case DatabaseObjectType.View:
                {
                    return $"DROP VIEW {BracketQualified(node.SchemaName, node.ObjectName)}";
                }

                case DatabaseObjectType.Database:
                {
                    return $"DROP DATABASE {Bracket(node.DatabaseName)}";
                }

                default:
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Wraps an identifier in brackets, escaping any closing bracket.
        /// </summary>
        /// <param name="identifier">The identifier to bracket.</param>
        /// <returns>The bracket-quoted identifier.</returns>
        private static string Bracket(string identifier)
        {
            return $"[{identifier.Replace("]", "]]")}]";
        }

        /// <summary>
        /// Builds a bracket-quoted "schema.name" reference.
        /// </summary>
        /// <param name="schema">The schema name.</param>
        /// <param name="name">The object name.</param>
        /// <returns>The qualified, bracket-quoted reference.</returns>
        private static string BracketQualified(string schema, string name)
        {
            return $"{Bracket(schema)}.{Bracket(name)}";
        }
    }
}
