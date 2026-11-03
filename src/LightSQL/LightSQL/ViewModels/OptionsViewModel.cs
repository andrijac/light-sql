using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightSQL.Models;
using LightSQL.Services;
using Microsoft.Win32;

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
        private string queriesFolder = string.Empty;

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
            queriesFolder = string.IsNullOrWhiteSpace(options.QueriesFolder) == true
                ? AppPaths.DefaultQueriesFolder
                : options.QueriesFolder;
            showLineNumbers = options.ShowLineNumbers;
            editorFontSize = options.EditorFontSize;
        }

        /// <summary>
        /// Whether the options were saved when the dialog closed.
        /// </summary>
        public bool Saved { get; private set; }

        /// <summary>
        /// Lets the user pick the queries folder.
        /// </summary>
        [RelayCommand]
        private void BrowseQueriesFolder()
        {
            OpenFolderDialog dialog = new()
            {
                Title = "Select queries folder",
                Multiselect = false
            };

            if (string.IsNullOrWhiteSpace(QueriesFolder) == false && Directory.Exists(QueriesFolder) == true)
            {
                dialog.InitialDirectory = QueriesFolder;
            }

            if (dialog.ShowDialog() == true)
            {
                QueriesFolder = dialog.FolderName;
            }
        }

        /// <summary>
        /// Persists the edited options.
        /// </summary>
        public void Save()
        {
            AppOptions options = new()
            {
                WarnOnUpdateDeleteWithoutWhere = WarnOnUpdateDeleteWithoutWhere,
                QueriesFolder = QueriesFolder,
                ShowLineNumbers = ShowLineNumbers,
                EditorFontSize = EditorFontSize
            };

            optionsService.Save(options);
            Saved = true;
        }
    }
}
