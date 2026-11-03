using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using LightSQL.Models;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// Presents the outcome of a query execution: the returned result grids and the
    /// Messages text.
    /// </summary>
    public partial class ResultsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string messages = string.Empty;

        [ObservableProperty]
        private int selectedTabIndex;

        /// <summary>
        /// Index of the "Results" tab in the results tab control.
        /// </summary>
        private const int ResultsTabIndex = 0;

        /// <summary>
        /// Index of the "Messages" tab in the results tab control.
        /// </summary>
        private const int MessagesTabIndex = 1;

        /// <summary>
        /// One <see cref="DataView"/> per returned result set, bound to a grid each.
        /// </summary>
        public ObservableCollection<DataView> ResultViews { get; } = new();

        /// <summary>
        /// Whether there is at least one result grid to show.
        /// </summary>
        public bool HasResults
        {
            get
            {
                return ResultViews.Count > 0;
            }
        }

        /// <summary>
        /// Loads an execution result, populating the grids and messages and selecting
        /// the most relevant tab.
        /// </summary>
        /// <param name="result">The execution result to display.</param>
        public void Load(QueryExecutionResult result)
        {
            ResultViews.Clear();

            foreach (DataTable table in result.ResultTables)
            {
                ResultViews.Add(table.DefaultView);
            }

            Messages = result.Messages;
            OnPropertyChanged(nameof(HasResults));

            // Show the grids when there are rows; otherwise show the Messages tab.
            SelectedTabIndex = ResultViews.Count > 0 ? ResultsTabIndex : MessagesTabIndex;
        }

        /// <summary>
        /// Sets the Messages text and switches to the Messages tab (used for errors
        /// and status text when there is nothing to grid).
        /// </summary>
        /// <param name="message">The message to display.</param>
        public void ShowMessage(string message)
        {
            ResultViews.Clear();
            Messages = message;
            OnPropertyChanged(nameof(HasResults));
            SelectedTabIndex = MessagesTabIndex;
        }

        /// <summary>
        /// Clears all results and messages.
        /// </summary>
        public void Clear()
        {
            ResultViews.Clear();
            Messages = string.Empty;
            OnPropertyChanged(nameof(HasResults));
        }
    }
}
