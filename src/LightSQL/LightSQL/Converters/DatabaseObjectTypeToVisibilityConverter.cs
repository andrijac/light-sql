using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LightSQL.Models;

namespace LightSQL.Converters
{
    /// <summary>
    /// Shows an element only for a particular <see cref="DatabaseObjectType"/>. The
    /// expected type is given as the converter parameter (its enum name, e.g.
    /// "Database"); the value is <see cref="Visibility.Visible"/> when the bound node's
    /// type matches and <see cref="Visibility.Collapsed"/> otherwise. Used to limit the
    /// Object Explorer's "Start MCP Server" menu item to database nodes.
    /// </summary>
    public sealed class DatabaseObjectTypeToVisibilityConverter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DatabaseObjectType objectType)
            {
                return Visibility.Collapsed;
            }

            if (Enum.TryParse(parameter as string, out DatabaseObjectType expected) == false)
            {
                return Visibility.Collapsed;
            }

            return objectType == expected ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
