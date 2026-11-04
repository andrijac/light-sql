using CommunityToolkit.Mvvm.ComponentModel;
using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// Backs the Options dialog, editing a working copy of <see cref="AppOptions"/>
    /// and persisting it on save.
    /// </summary>
    public partial class OptionsViewModel : ObservableObject
    {
        private readonly IOptionsService optionsService;

        [ObservableProperty]
        private bool warnOnUpdateDeleteWithoutWhere;

        [ObservableProperty]
        private bool showLineNumbers;

        [ObservableProperty]
        private double editorFontSize;

        /// <summary>
        /// Creates the view model, seeded from the current options.
        /// </summary>
        /// <param name="optionsService">Service used to load and persist options.</param>
        public OptionsViewModel(IOptionsService optionsService)
        {
            this.optionsService = optionsService;

            AppOptions options = optionsService.Current;

            warnOnUpdateDeleteWithoutWhere = options.WarnOnUpdateDeleteWithoutWhere;
            showLineNumbers = options.ShowLineNumbers;
            editorFontSize = options.EditorFontSize;
        }

        /// <summary>
        /// Whether the options were saved when the dialog closed.
        /// </summary>
        public bool Saved { get; private set; }

        /// <summary>
        /// Persists the edited options.
        /// </summary>
        public void Save()
        {
            AppOptions options = new()
            {
                WarnOnUpdateDeleteWithoutWhere = WarnOnUpdateDeleteWithoutWhere,
                ShowLineNumbers = ShowLineNumbers,
                EditorFontSize = EditorFontSize
            };

            optionsService.Save(options);
            Saved = true;
        }
    }
}