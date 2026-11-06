using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using LightSQL.Editor;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests that the SQL highlighting picks up theme colours.
    /// </summary>
    public class SqlHighlightingTests
    {
        [Fact]
        public void GetColorNames_ListsTheXshdColours()
        {
            IReadOnlyList<string> names = SqlHighlighting.GetColorNames();

            Assert.Equal(new[] { "Comment", "String", "Keyword", "DataType", "Function", "Variable", "Number" }, names);
        }

        [Fact]
        public void Create_AppliesGivenColours()
        {
            Dictionary<string, Color> colors = new()
            {
                ["Keyword"] = Colors.Orange,
                ["Comment"] = Colors.Pink
            };

            IHighlightingDefinition? definition = SqlHighlighting.Create(colors);

            Assert.NotNull(definition);
            Assert.Equal(Colors.Orange, definition.GetNamedColor("Keyword").Foreground.GetColor(null!));
            Assert.Equal(Colors.Pink, definition.GetNamedColor("Comment").Foreground.GetColor(null!));
        }

        [Fact]
        public void Create_MissingColours_KeepXshdDefaults()
        {
            IHighlightingDefinition? definition = SqlHighlighting.Create(new Dictionary<string, Color>());

            Assert.NotNull(definition);
            Assert.Equal(Color.FromRgb(0x00, 0x00, 0xFF), definition.GetNamedColor("Keyword").Foreground.GetColor(null!));
        }
    }
}
