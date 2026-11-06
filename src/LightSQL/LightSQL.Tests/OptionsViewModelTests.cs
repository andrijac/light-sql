using LightSQL.Models;
using LightSQL.ViewModels;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the theme choice in the Options dialog: preview, save and cancel.
    /// </summary>
    public class OptionsViewModelTests
    {
        private readonly FakeOptionsService optionsService = new();

        private readonly FakeThemeService themeService = new();

        [Fact]
        public void Themes_OfferEveryTheme()
        {
            OptionsViewModel viewModel = new(optionsService, themeService);

            List<AppTheme> offered = viewModel.Themes.Select(option => option.Theme).ToList();

            Assert.Equal(Enum.GetValues<AppTheme>().OrderBy(t => t), offered.OrderBy(t => t));
            Assert.All(viewModel.Themes, option => Assert.False(string.IsNullOrWhiteSpace(option.DisplayName)));
        }

        [Fact]
        public void NewViewModel_ShowsSavedTheme_WithoutApplyingIt()
        {
            optionsService.Save(new AppOptions { Theme = AppTheme.Classic });

            OptionsViewModel viewModel = new(optionsService, themeService);

            Assert.Equal(AppTheme.Classic, viewModel.Theme);
            Assert.Empty(themeService.AppliedThemes);
        }

        [Fact]
        public void ChangingTheme_PreviewsIt()
        {
            OptionsViewModel viewModel = new(optionsService, themeService);

            viewModel.Theme = AppTheme.Gradient;

            Assert.Equal(new[] { AppTheme.Gradient }, themeService.AppliedThemes);
        }

        [Fact]
        public void Save_PersistsTheme()
        {
            OptionsViewModel viewModel = new(optionsService, themeService);
            viewModel.Theme = AppTheme.Dark;

            viewModel.Save();
            viewModel.RevertThemePreview();

            Assert.Equal(AppTheme.Dark, optionsService.Current.Theme);
            Assert.Equal(AppTheme.Dark, themeService.SelectedTheme);
        }

        [Fact]
        public void ClosingWithoutSave_RestoresPreviousTheme()
        {
            themeService.SelectedTheme = AppTheme.Light;
            OptionsViewModel viewModel = new(optionsService, themeService);
            viewModel.Theme = AppTheme.Dark;

            viewModel.RevertThemePreview();

            Assert.Equal(AppTheme.Light, themeService.SelectedTheme);
            Assert.Equal(AppTheme.System, optionsService.Current.Theme);
        }

        [Fact]
        public void ClosingWithoutChanges_DoesNotReapplyTheme()
        {
            OptionsViewModel viewModel = new(optionsService, themeService);

            viewModel.RevertThemePreview();

            Assert.Empty(themeService.AppliedThemes);
        }
    }
}
