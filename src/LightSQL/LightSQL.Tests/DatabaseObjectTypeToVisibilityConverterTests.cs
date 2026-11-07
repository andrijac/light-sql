using System.Globalization;
using System.Windows;
using LightSQL.Converters;
using LightSQL.Models;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests that <see cref="DatabaseObjectTypeToVisibilityConverter"/> shows the element
    /// only for the object type named in the converter parameter.
    /// </summary>
    public class DatabaseObjectTypeToVisibilityConverterTests
    {
        private readonly DatabaseObjectTypeToVisibilityConverter converter = new();

        [Fact]
        public void Convert_MatchingType_IsVisible()
        {
            object result = converter.Convert(DatabaseObjectType.Database, typeof(Visibility), "Database", CultureInfo.InvariantCulture);

            Assert.Equal(Visibility.Visible, result);
        }

        [Theory]
        [InlineData(DatabaseObjectType.Server)]
        [InlineData(DatabaseObjectType.Table)]
        [InlineData(DatabaseObjectType.ViewsFolder)]
        [InlineData(DatabaseObjectType.Column)]
        public void Convert_OtherType_IsCollapsed(DatabaseObjectType objectType)
        {
            object result = converter.Convert(objectType, typeof(Visibility), "Database", CultureInfo.InvariantCulture);

            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void Convert_NonEnumValue_IsCollapsed()
        {
            object result = converter.Convert("not an enum", typeof(Visibility), "Database", CultureInfo.InvariantCulture);

            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void Convert_UnknownParameter_IsCollapsed()
        {
            object result = converter.Convert(DatabaseObjectType.Database, typeof(Visibility), "Nonsense", CultureInfo.InvariantCulture);

            Assert.Equal(Visibility.Collapsed, result);
        }
    }
}
