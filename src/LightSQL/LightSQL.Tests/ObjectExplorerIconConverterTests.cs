using System.Globalization;
using System.Windows;
using System.Windows.Media;
using LightSQL.Converters;
using LightSQL.Models;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests that every Object Explorer node type gets an icon, folders switch between
    /// open and closed, and every icon key exists in the icon resource dictionary.
    /// </summary>
    public class ObjectExplorerIconConverterTests
    {
        [Theory]
        [InlineData(DatabaseObjectType.Server, ObjectExplorerIconConverter.ServerIconKey)]
        [InlineData(DatabaseObjectType.Database, ObjectExplorerIconConverter.DatabaseIconKey)]
        [InlineData(DatabaseObjectType.Table, ObjectExplorerIconConverter.TableIconKey)]
        [InlineData(DatabaseObjectType.View, ObjectExplorerIconConverter.ViewIconKey)]
        [InlineData(DatabaseObjectType.Column, ObjectExplorerIconConverter.ColumnIconKey)]
        [InlineData(DatabaseObjectType.Placeholder, ObjectExplorerIconConverter.LoadingIconKey)]
        [InlineData(DatabaseObjectType.Error, ObjectExplorerIconConverter.ErrorIconKey)]
        public void GetIconKey_ObjectTypes_ReturnMatchingIcon(DatabaseObjectType objectType, string expectedKey)
        {
            Assert.Equal(expectedKey, ObjectExplorerIconConverter.GetIconKey(objectType, false));
            Assert.Equal(expectedKey, ObjectExplorerIconConverter.GetIconKey(objectType, true));
        }

        [Theory]
        [InlineData(DatabaseObjectType.DatabasesFolder)]
        [InlineData(DatabaseObjectType.TablesFolder)]
        [InlineData(DatabaseObjectType.ViewsFolder)]
        [InlineData(DatabaseObjectType.ColumnsFolder)]
        public void GetIconKey_Folders_OpenWhenExpanded(DatabaseObjectType objectType)
        {
            Assert.Equal(ObjectExplorerIconConverter.FolderClosedIconKey, ObjectExplorerIconConverter.GetIconKey(objectType, false));
            Assert.Equal(ObjectExplorerIconConverter.FolderOpenIconKey, ObjectExplorerIconConverter.GetIconKey(objectType, true));
        }

        [Fact]
        public void GetIconKey_EveryObjectType_HasAnIcon()
        {
            foreach (DatabaseObjectType objectType in Enum.GetValues<DatabaseObjectType>())
            {
                Assert.NotNull(ObjectExplorerIconConverter.GetIconKey(objectType, false));
            }
        }

        [Fact]
        public void GetIconKey_UnsetValue_ReturnsNull()
        {
            Assert.Null(ObjectExplorerIconConverter.GetIconKey(default, false));
        }

        [Fact]
        public void Convert_NonObjectTypeValue_ReturnsNull()
        {
            ObjectExplorerIconConverter converter = new();

            object? result = converter.Convert(new object[] { "not a type", true }, typeof(ImageSource), null!, CultureInfo.InvariantCulture);

            Assert.Null(result);
        }

        [Fact]
        public void IconDictionary_ContainsAnImageForEveryKey()
        {
            string[] keys =
            {
                ObjectExplorerIconConverter.ServerIconKey,
                ObjectExplorerIconConverter.FolderClosedIconKey,
                ObjectExplorerIconConverter.FolderOpenIconKey,
                ObjectExplorerIconConverter.DatabaseIconKey,
                ObjectExplorerIconConverter.TableIconKey,
                ObjectExplorerIconConverter.ViewIconKey,
                ObjectExplorerIconConverter.ColumnIconKey,
                ObjectExplorerIconConverter.LoadingIconKey,
                ObjectExplorerIconConverter.ErrorIconKey
            };

            List<string> missing = RunOnStaThread(() =>
            {
                ResourceDictionary icons = (ResourceDictionary)Application.LoadComponent(
                    new Uri("/LightSQL;component/Resources/ObjectExplorerIcons.xaml", UriKind.Relative));

                List<string> notFound = new();

                foreach (string key in keys)
                {
                    if (icons[key] is not ImageSource image || image.Width != 16d || image.Height != 16d)
                    {
                        notFound.Add(key);
                    }
                }

                return notFound;
            });

            Assert.Empty(missing);
        }

        /// <summary>
        /// Runs a function on a new STA thread (required to load WPF XAML) and returns
        /// its result, rethrowing any exception.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="function">The function to run.</param>
        /// <returns>The function's result.</returns>
        private static T RunOnStaThread<T>(Func<T> function)
        {
            T result = default!;
            Exception? failure = null;

            Thread thread = new(() =>
            {
                try
                {
                    result = function();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure is not null)
            {
                throw new InvalidOperationException("STA work failed.", failure);
            }

            return result;
        }
    }
}