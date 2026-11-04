using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LightSQL.Models;

namespace LightSQL.Converters
{
    /// <summary>
    /// Picks the Object Explorer icon for a tree node. Expects two bound values:
    /// the node's <see cref="DatabaseObjectType"/> and whether it is expanded (folders
    /// show an open folder while expanded). Returns the matching image resource from
    /// <c>Resources/ObjectExplorerIcons.xaml</c>, or null when there is none.
    /// </summary>
    public sealed class ObjectExplorerIconConverter : IMultiValueConverter
    {
        /// <summary>
        /// Resource key of the server icon.
        /// </summary>
        public const string ServerIconKey = "Icon.Server";

        /// <summary>
        /// Resource key of the closed (collapsed) folder icon.
        /// </summary>
        public const string FolderClosedIconKey = "Icon.FolderClosed";

        /// <summary>
        /// Resource key of the open (expanded) folder icon.
        /// </summary>
        public const string FolderOpenIconKey = "Icon.FolderOpen";

        /// <summary>
        /// Resource key of the database icon.
        /// </summary>
        public const string DatabaseIconKey = "Icon.Database";

        /// <summary>
        /// Resource key of the table icon.
        /// </summary>
        public const string TableIconKey = "Icon.Table";

        /// <summary>
        /// Resource key of the view icon.
        /// </summary>
        public const string ViewIconKey = "Icon.View";

        /// <summary>
        /// Resource key of the column icon.
        /// </summary>
        public const string ColumnIconKey = "Icon.Column";

        /// <summary>
        /// Resource key of the "Loading..." placeholder icon.
        /// </summary>
        public const string LoadingIconKey = "Icon.Loading";

        /// <summary>
        /// Resource key of the error icon.
        /// </summary>
        public const string ErrorIconKey = "Icon.Error";

        /// <summary>
        /// Returns the resource key of the icon for a node type.
        /// </summary>
        /// <param name="objectType">The kind of node.</param>
        /// <param name="isExpanded">Whether the node is expanded (affects folders only).</param>
        /// <returns>The icon's resource key, or null for an unknown type.</returns>
        public static string? GetIconKey(DatabaseObjectType objectType, bool isExpanded)
        {
            switch (objectType)
            {
                case DatabaseObjectType.Server:
                {
                    return ServerIconKey;
                }

                case DatabaseObjectType.DatabasesFolder:
                case DatabaseObjectType.TablesFolder:
                case DatabaseObjectType.ViewsFolder:
                case DatabaseObjectType.ColumnsFolder:
                {
                    return isExpanded == true ? FolderOpenIconKey : FolderClosedIconKey;
                }

                case DatabaseObjectType.Database:
                {
                    return DatabaseIconKey;
                }

                case DatabaseObjectType.Table:
                {
                    return TableIconKey;
                }

                case DatabaseObjectType.View:
                {
                    return ViewIconKey;
                }

                case DatabaseObjectType.Column:
                {
                    return ColumnIconKey;
                }

                case DatabaseObjectType.Placeholder:
                {
                    return LoadingIconKey;
                }

                case DatabaseObjectType.Error:
                {
                    return ErrorIconKey;
                }

                default:
                {
                    return null;
                }
            }
        }

        /// <inheritdoc />
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 1 || values[0] is not DatabaseObjectType objectType)
            {
                return null;
            }

            bool isExpanded = values.Length > 1 && values[1] is bool expanded && expanded == true;
            string? key = GetIconKey(objectType, isExpanded);

            if (key is null || Application.Current is null)
            {
                return null;
            }

            return Application.Current.TryFindResource(key);
        }

        /// <inheritdoc />
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("The Object Explorer icon converter is one-way.");
        }
    }
}