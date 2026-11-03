using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LightSQL.Converters
{
    /// <summary>
    /// Converts a boolean to <see cref="Visibility"/> inversely: false becomes
    /// Visible and true becomes Collapsed.
    /// </summary>
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool boolean && boolean == true;

            return flag == true ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && visibility != Visibility.Visible;
        }
    }
}
