using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace LightSQL.Editor
{
    /// <summary>
    /// Builds the T-SQL highlighting definition from the embedded SqlSyntax.xshd,
    /// with its named colours (Keyword, String, ...) replaced by the active theme's
    /// colours. The palette key of a colour is <see cref="ResourceKeyPrefix"/> + its
    /// name, e.g. "Theme.Syntax.Keyword".
    /// </summary>
    public static class SqlHighlighting
    {
        /// <summary>
        /// Prefix of the palette resource keys that hold the syntax colours.
        /// </summary>
        public const string ResourceKeyPrefix = "Theme.Syntax.";

        /// <summary>
        /// Manifest resource name of the embedded highlighting definition.
        /// </summary>
        private const string ResourceName = "LightSQL.Editor.SqlSyntax.xshd";

        /// <summary>
        /// Gets the names of the colours defined in SqlSyntax.xshd. Every palette must
        /// define a "Theme.Syntax.{name}" brush for each of them.
        /// </summary>
        /// <returns>The colour names, or an empty list if the resource is missing.</returns>
        public static IReadOnlyList<string> GetColorNames()
        {
            XshdSyntaxDefinition? xshd = LoadXshd();

            if (xshd is null)
            {
                return Array.Empty<string>();
            }

            return xshd.Elements.OfType<XshdColor>().Select(color => color.Name).ToList();
        }

        /// <summary>
        /// Creates the highlighting definition with the given colours. Colours not in
        /// the dictionary keep the value from SqlSyntax.xshd.
        /// </summary>
        /// <param name="colors">Colour per xshd colour name (e.g. "Keyword").</param>
        /// <returns>The definition, or null if the embedded resource is missing.</returns>
        public static IHighlightingDefinition? Create(IReadOnlyDictionary<string, Color> colors)
        {
            XshdSyntaxDefinition? xshd = LoadXshd();

            if (xshd is null)
            {
                return null;
            }

            foreach (XshdColor xshdColor in xshd.Elements.OfType<XshdColor>())
            {
                if (xshdColor.Name is not null && colors.TryGetValue(xshdColor.Name, out Color color) == true)
                {
                    xshdColor.Foreground = new SimpleHighlightingBrush(color);
                }
            }

            return HighlightingLoader.Load(xshd, HighlightingManager.Instance);
        }

        /// <summary>
        /// Reads SqlSyntax.xshd from the assembly resources.
        /// </summary>
        /// <returns>The parsed xshd, or null if the resource is missing.</returns>
        private static XshdSyntaxDefinition? LoadXshd()
        {
            Assembly assembly = typeof(SqlHighlighting).Assembly;

            using Stream? stream = assembly.GetManifestResourceStream(ResourceName);

            if (stream is null)
            {
                return null;
            }

            using XmlReader reader = XmlReader.Create(stream);

            return HighlightingLoader.LoadXshd(reader);
        }
    }
}
