using System.Windows;
using LightSQL.Models;
using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests theme resolution (Auto -> Light/Dark), palette selection and swapping
    /// the palette dictionary at runtime.
    /// </summary>
    public class ThemeServiceTests
    {
        [Theory]
        [InlineData(AppTheme.Light, true, AppTheme.Light)]
        [InlineData(AppTheme.Light, false, AppTheme.Light)]
        [InlineData(AppTheme.Dark, true, AppTheme.Dark)]
        [InlineData(AppTheme.Classic, false, AppTheme.Classic)]
        [InlineData(AppTheme.Gradient, true, AppTheme.Gradient)]
        [InlineData(AppTheme.System, true, AppTheme.Light)]
        [InlineData(AppTheme.System, false, AppTheme.Dark)]
        public void ResolveEffectiveTheme_ReturnsConcreteTheme(AppTheme selected, bool systemUsesLight, AppTheme expected)
        {
            Assert.Equal(expected, ThemeService.ResolveEffectiveTheme(selected, systemUsesLight));
        }

        [Fact]
        public void ResolveEffectiveTheme_UnsetValue_FollowsWindows()
        {
            Assert.Equal(AppTheme.Dark, ThemeService.ResolveEffectiveTheme(default, false));
        }

        [Fact]
        public void GetPaletteUri_EveryConcreteTheme_HasItsOwnPalette()
        {
            AppTheme[] concrete = { AppTheme.Light, AppTheme.Dark, AppTheme.Classic, AppTheme.Gradient };

            List<string> uris = concrete.Select(theme => ThemeService.GetPaletteUri(theme).OriginalString).ToList();

            Assert.Equal(concrete.Length, uris.Distinct().Count());
            Assert.Contains("/Themes/Palettes/Dark.xaml", uris[1]);
        }

        [Fact]
        public void GetPaletteUri_System_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ThemeService.GetPaletteUri(AppTheme.System));
        }

        [Theory]
        [InlineData(AppTheme.Light, false)]
        [InlineData(AppTheme.Classic, false)]
        [InlineData(AppTheme.Dark, true)]
        [InlineData(AppTheme.Gradient, true)]
        public void IsDarkTheme_DarkBackgroundThemes(AppTheme theme, bool expected)
        {
            Assert.Equal(expected, ThemeService.IsDarkTheme(theme));
        }

        [Fact]
        public void ApplyTheme_ReplacesPaletteAndKeepsOtherDictionaries()
        {
            (string? name, int paletteCount, bool otherKept) = StaThread.Run(() =>
            {
                ResourceDictionary resources = new();
                ResourceDictionary other = new() { ["Other"] = "kept" };
                resources.MergedDictionaries.Add(other);

                ThemeService service = new(new FakeSystemThemeProvider(), resources);
                service.ApplyTheme(AppTheme.Light);
                service.ApplyTheme(AppTheme.Dark);

                int palettes = resources.MergedDictionaries.Count(d => d.Contains(ThemeService.ThemeNameKey));

                return (resources[ThemeService.ThemeNameKey] as string, palettes, resources.MergedDictionaries.Contains(other));
            });

            Assert.Equal("Dark", name);
            Assert.Equal(1, paletteCount);
            Assert.True(otherKept);
        }

        [Fact]
        public void ApplyTheme_System_UsesWindowsMode()
        {
            (string? name, AppTheme effective, bool isDark) = StaThread.Run(() =>
            {
                FakeSystemThemeProvider system = new();
                system.SwitchTo(false);

                ResourceDictionary resources = new();
                ThemeService service = new(system, resources);
                service.ApplyTheme(AppTheme.System);

                return (resources[ThemeService.ThemeNameKey] as string, service.EffectiveTheme, service.IsDark);
            });

            Assert.Equal("Dark", name);
            Assert.Equal(AppTheme.Dark, effective);
            Assert.True(isDark);
        }

        [Fact]
        public void WindowsModeChange_ReappliesAutoTheme()
        {
            string? name = StaThread.Run(() =>
            {
                FakeSystemThemeProvider system = new();
                ResourceDictionary resources = new();
                ThemeService service = new(system, resources);
                service.ApplyTheme(AppTheme.System);

                system.SwitchTo(false);

                return resources[ThemeService.ThemeNameKey] as string;
            });

            Assert.Equal("Dark", name);
        }

        [Fact]
        public void WindowsModeChange_KeepsExplicitTheme()
        {
            string? name = StaThread.Run(() =>
            {
                FakeSystemThemeProvider system = new();
                ResourceDictionary resources = new();
                ThemeService service = new(system, resources);
                service.ApplyTheme(AppTheme.Classic);

                system.SwitchTo(false);

                return resources[ThemeService.ThemeNameKey] as string;
            });

            Assert.Equal("Classic", name);
        }

        [Fact]
        public void ApplyTheme_UndefinedValue_FallsBackToAuto()
        {
            AppTheme selected = StaThread.Run(() =>
            {
                ThemeService service = new(new FakeSystemThemeProvider(), new ResourceDictionary());
                service.ApplyTheme((AppTheme)99);

                return service.SelectedTheme;
            });

            Assert.Equal(AppTheme.System, selected);
        }
    }
}
