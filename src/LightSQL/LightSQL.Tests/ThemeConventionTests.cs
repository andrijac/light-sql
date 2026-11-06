using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using LightSQL.Editor;
using LightSQL.Models;
using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Guards the theming rules for all current and future UI:
    /// every palette defines the same keys, every referenced theme key exists,
    /// views contain no hard-coded colours, and every window uses the themed style.
    /// </summary>
    public class ThemeConventionTests
    {
        /// <summary>
        /// The palettes that must define identical keys.
        /// </summary>
        public static readonly TheoryData<AppTheme> ConcreteThemes = new()
        {
            AppTheme.Light,
            AppTheme.Dark,
            AppTheme.Classic,
            AppTheme.Gradient
        };

        /// <summary>
        /// Attributes that take a colour/brush; a literal colour in one of these is a
        /// theming violation.
        /// </summary>
        private static readonly Regex colorAttributePattern = new(
            "\\b(Background|Foreground|BorderBrush|Fill|Stroke|Color|Value|CaretBrush|SelectionBrush|LineNumbersForeground)=\"([^\"{][^\"]*)\"",
            RegexOptions.Compiled);

        /// <summary>
        /// A theme resource key used in XAML (DynamicResource/StaticResource) or code.
        /// </summary>
        private static readonly Regex themeKeyPattern = new(
            "(?:DynamicResource|StaticResource)\\s+(Theme\\.[A-Za-z0-9_.]+)|\"(Theme\\.[A-Za-z0-9_.]*[A-Za-z0-9_])\"",
            RegexOptions.Compiled);

        [Theory]
        [MemberData(nameof(ConcreteThemes))]
        public void Palette_DefinesTheSameKeysAsLight(AppTheme theme)
        {
            (List<string> missing, List<string> extra) = StaThread.Run(() =>
            {
                HashSet<string> lightKeys = GetKeys(LoadPalette(AppTheme.Light));
                HashSet<string> keys = GetKeys(LoadPalette(theme));

                return (lightKeys.Except(keys).ToList(), keys.Except(lightKeys).ToList());
            });

            Assert.Empty(missing);
            Assert.Empty(extra);
        }

        [Theory]
        [MemberData(nameof(ConcreteThemes))]
        public void Palette_NameMatchesTheme(AppTheme theme)
        {
            string? name = StaThread.Run(() => LoadPalette(theme)[ThemeService.ThemeNameKey] as string);

            Assert.Equal(theme.ToString(), name);
        }

        [Theory]
        [MemberData(nameof(ConcreteThemes))]
        public void Palette_HasABrushForEverySyntaxColour(AppTheme theme)
        {
            List<string> missing = StaThread.Run(() =>
            {
                ResourceDictionary palette = LoadPalette(theme);

                return SqlHighlighting.GetColorNames()
                    .Select(name => SqlHighlighting.ResourceKeyPrefix + name)
                    .Where(key => palette[key] is not SolidColorBrush)
                    .ToList();
            });

            Assert.Empty(missing);
        }

        [Fact]
        public void ControlStyles_Load()
        {
            int count = StaThread.Run(() => LoadControls().Count);

            Assert.True(count > 0);
        }

        [Fact]
        public void EveryReferencedThemeKey_Exists()
        {
            List<string> referenced = new();

            foreach (string file in GetSourceFiles("*.xaml").Concat(GetSourceFiles("*.cs")))
            {
                foreach (Match match in themeKeyPattern.Matches(File.ReadAllText(file)))
                {
                    string key = match.Groups[1].Success == true ? match.Groups[1].Value : match.Groups[2].Value;
                    referenced.Add(key);
                }
            }

            List<string> missing = StaThread.Run(() =>
            {
                HashSet<string> defined = GetKeys(LoadPalette(AppTheme.Light));
                defined.UnionWith(GetKeys(LoadControls()));

                return referenced.Distinct().Where(key => defined.Contains(key) == false).ToList();
            });

            Assert.NotEmpty(referenced);
            Assert.Empty(missing);
        }

        [Fact]
        public void Views_HaveNoHardCodedColours()
        {
            List<string> violations = new();

            foreach (string file in GetSourceFiles("*.xaml"))
            {
                string relative = Path.GetRelativePath(GetProjectDirectory(), file).Replace('\\', '/');

                // Palettes define the colours; the Object Explorer icons are images.
                if (relative.StartsWith("Themes/", StringComparison.Ordinal) == true
                    || relative == "Resources/ObjectExplorerIcons.xaml")
                {
                    continue;
                }

                foreach (Match match in colorAttributePattern.Matches(File.ReadAllText(file)))
                {
                    string value = match.Groups[2].Value;

                    if (value != "Transparent" && IsColor(value) == true)
                    {
                        violations.Add($"{relative}: {match.Value}");
                    }
                }
            }

            Assert.Empty(violations);
        }

        [Fact]
        public void EveryWindow_UsesTheThemedWindowStyle()
        {
            List<string> unthemed = new();

            foreach (string file in GetSourceFiles("*.xaml"))
            {
                string text = File.ReadAllText(file).TrimStart('\uFEFF', ' ', '\r', '\n');

                if (text.StartsWith("<Window ", StringComparison.Ordinal) == true
                    && text.Contains("Style=\"{DynamicResource Theme.WindowStyle}\"") == false)
                {
                    unthemed.Add(Path.GetFileName(file));
                }
            }

            Assert.Empty(unthemed);
        }

        [Fact]
        public void InlineStylesOfThemedControls_AreBasedOnTheTheme()
        {
            // A Style without BasedOn replaces the themed implicit style completely,
            // so the control falls back to the default (light) WPF look.
            string controlsXaml = File.ReadAllText(Path.Combine(GetProjectDirectory(), "Themes", "Controls.xaml"));
            HashSet<string> themedTypes = Regex.Matches(controlsXaml, "<Style TargetType=\"(\\w+)\">")
                .Select(match => match.Groups[1].Value)
                .ToHashSet();

            List<string> violations = new();

            foreach (string file in GetSourceFiles("*.xaml"))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}") == true)
                {
                    continue;
                }

                foreach (Match match in Regex.Matches(File.ReadAllText(file), "<Style\\b[^>]*>"))
                {
                    Match target = Regex.Match(match.Value, "TargetType=\"(?:\\{x:Type )?(\\w+)\\}?\"");

                    if (target.Success == true
                        && themedTypes.Contains(target.Groups[1].Value) == true
                        && match.Value.Contains("BasedOn=") == false)
                    {
                        violations.Add($"{Path.GetFileName(file)}: {match.Value}");
                    }
                }
            }

            Assert.Contains("TreeViewItem", themedTypes);
            Assert.Empty(violations);
        }

        /// <summary>
        /// Loads a palette dictionary from the application assembly.
        /// </summary>
        /// <param name="theme">A concrete theme.</param>
        /// <returns>The palette.</returns>
        private static ResourceDictionary LoadPalette(AppTheme theme)
        {
            return (ResourceDictionary)Application.LoadComponent(ThemeService.GetPaletteUri(theme));
        }

        /// <summary>
        /// Loads Themes/Controls.xaml from the application assembly.
        /// </summary>
        /// <returns>The control styles.</returns>
        private static ResourceDictionary LoadControls()
        {
            return (ResourceDictionary)Application.LoadComponent(
                new Uri("/LightSQL;component/Themes/Controls.xaml", UriKind.Relative));
        }

        /// <summary>
        /// Gets the string keys of a dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary.</param>
        /// <returns>Its string keys.</returns>
        private static HashSet<string> GetKeys(ResourceDictionary dictionary)
        {
            return dictionary.Keys.OfType<string>().ToHashSet();
        }

        /// <summary>
        /// Whether a XAML attribute value is a literal colour (e.g. "#FF0000", "Gray").
        /// </summary>
        /// <param name="value">The attribute value.</param>
        /// <returns>True when it parses as a colour.</returns>
        private static bool IsColor(string value)
        {
            try
            {
                return ColorConverter.ConvertFromString(value) is Color;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the application's source files (not build output).
        /// </summary>
        /// <param name="pattern">File pattern, e.g. "*.xaml".</param>
        /// <returns>Full paths of the matching files.</returns>
        private static IEnumerable<string> GetSourceFiles(string pattern)
        {
            string projectDirectory = GetProjectDirectory();

            return Directory.EnumerateFiles(projectDirectory, pattern, SearchOption.AllDirectories)
                .Where(file => file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") == false
                    && file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") == false);
        }

        /// <summary>
        /// Finds the LightSQL application project folder by walking up from the test
        /// output folder to the solution folder.
        /// </summary>
        /// <returns>The full path of src/LightSQL/LightSQL.</returns>
        private static string GetProjectDirectory()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "LightSQL.slnx")) == true)
                {
                    return Path.Combine(directory.FullName, "LightSQL");
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find LightSQL.slnx above the test output folder.");
        }
    }
}
