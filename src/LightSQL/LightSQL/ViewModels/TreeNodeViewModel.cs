using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LightSQL.Models;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// A single node in the Object Explorer tree. Children are loaded lazily the
    /// first time the node is expanded.
    /// </summary>
    public partial class TreeNodeViewModel : ObservableObject
    {
        private readonly Func<Task<IReadOnlyList<TreeNodeViewModel>>>? childLoader;

        private bool isLoaded;

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private bool isExpanded;

        [ObservableProperty]
        private bool isLoading;

        /// <summary>
        /// Creates a tree node.
        /// </summary>
        /// <param name="title">The display text.</param>
        /// <param name="objectType">The kind of node.</param>
        /// <param name="childLoader">
        /// Optional loader for child nodes. When provided, the node is expandable and
        /// shows a placeholder until first expanded.
        /// </param>
        public TreeNodeViewModel(string title, DatabaseObjectType objectType, Func<Task<IReadOnlyList<TreeNodeViewModel>>>? childLoader = null)
        {
            this.title = title;
            this.childLoader = childLoader;

            ObjectType = objectType;

            if (childLoader is not null)
            {
                Children.Add(CreatePlaceholder());
            }
        }

        /// <summary>
        /// The kind of object this node represents.
        /// </summary>
        public DatabaseObjectType ObjectType { get; init; }

        /// <summary>
        /// The database that owns this node (for database/table/view/column nodes).
        /// </summary>
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>
        /// The schema of this node (for table/view/column nodes).
        /// </summary>
        public string SchemaName { get; set; } = string.Empty;

        /// <summary>
        /// The object name (for table/view/column nodes).
        /// </summary>
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// The parent node, or null for a root node. Set when children are loaded.
        /// </summary>
        public TreeNodeViewModel? Parent { get; set; }

        /// <summary>
        /// The child nodes (lazily populated).
        /// </summary>
        public ObservableCollection<TreeNodeViewModel> Children { get; } = new();

        /// <summary>
        /// Whether this node can be expanded to load children.
        /// </summary>
        public bool IsExpandable
        {
            get
            {
                return childLoader is not null;
            }
        }

        /// <summary>
        /// Reloads the node's children from scratch.
        /// </summary>
        /// <returns>A task that completes when the refresh finishes.</returns>
        public async Task RefreshAsync()
        {
            isLoaded = false;
            Children.Clear();

            if (childLoader is not null)
            {
                Children.Add(CreatePlaceholder());

                if (IsExpanded == true)
                {
                    await LoadChildrenAsync();
                }
            }
        }

        /// <summary>
        /// Triggers lazy loading the first time the node is expanded.
        /// </summary>
        /// <param name="value">The new expanded state.</param>
        partial void OnIsExpandedChanged(bool value)
        {
            if (value == true && isLoaded == false && childLoader is not null)
            {
                _ = LoadChildrenAsync();
            }
        }

        /// <summary>
        /// Loads the node's children, replacing the placeholder. Errors are surfaced
        /// as a child node rather than thrown.
        /// </summary>
        /// <returns>A task that completes when loading finishes.</returns>
        private async Task LoadChildrenAsync()
        {
            if (childLoader is null)
            {
                return;
            }

            IsLoading = true;

            try
            {
                IReadOnlyList<TreeNodeViewModel> items = await childLoader();

                Children.Clear();

                foreach (TreeNodeViewModel item in items)
                {
                    item.Parent = this;
                    Children.Add(item);
                }

                isLoaded = true;
            }
            catch (Exception ex)
            {
                Children.Clear();
                Children.Add(new TreeNodeViewModel($"(error: {ex.Message})", DatabaseObjectType.Error));
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Creates the temporary "Loading..." placeholder shown before children load.
        /// </summary>
        /// <returns>A placeholder node.</returns>
        private static TreeNodeViewModel CreatePlaceholder()
        {
            return new TreeNodeViewModel("Loading...", DatabaseObjectType.Placeholder);
        }
    }
}
