using System.Globalization;
using System.Windows.Data;

namespace LightSQL.Converters
{
    /// <summary>
    /// Returns the logical negation of a boolean value. Useful for binding an
    /// "enabled" state to a "busy" flag.
    /// </summary>
    public sealed class InverseBooleanConverter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool boolean && boolean == true;

            return flag == false;
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool boolean && boolean == true;

            return flag == false;
        }
    }
}
