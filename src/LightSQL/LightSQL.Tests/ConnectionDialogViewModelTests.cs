using LightSQL.Models;
using LightSQL.ViewModels;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests that picking a saved connection in the connection dialog loads it and
    /// lets the user connect without any extra step.
    /// </summary>
    public class ConnectionDialogViewModelTests
    {
        [Fact]
        public void SelectingSavedConnection_LoadsFormFields()
        {
            ConnectionProfile saved = new()
            {
                Name = "Prod",
                Server = "sql01",
                AuthenticationType = AuthenticationType.SqlServer,
                UserId = "app",
                Database = "Sales",
                Encrypt = false,
                TrustServerCertificate = false,
                SavePassword = true,
                Password = "secret"
            };

            ConnectionDialogViewModel viewModel = CreateViewModel(saved);

            viewModel.SelectedSavedConnection = saved;

            Assert.Equal("Prod", viewModel.Name);
            Assert.False(viewModel.UseRawConnectionString);
            Assert.Equal("sql01", viewModel.Server);
            Assert.Equal(AuthenticationType.SqlServer, viewModel.AuthenticationType);
            Assert.Equal("app", viewModel.UserId);
            Assert.Equal("Sales", viewModel.Database);
            Assert.False(viewModel.Encrypt);
            Assert.False(viewModel.TrustServerCertificate);
            Assert.True(viewModel.SavePassword);
            Assert.Equal("secret", viewModel.Password);
        }

        [Fact]
        public void SelectingSavedConnection_RaisesSavedConnectionLoaded()
        {
            ConnectionProfile saved = new() { Name = "Local", Server = "localhost" };
            ConnectionDialogViewModel viewModel = CreateViewModel(saved);
            int raised = 0;
            viewModel.SavedConnectionLoaded += (sender, e) => raised++;

            viewModel.SelectedSavedConnection = saved;

            Assert.Equal(1, raised);
        }

        [Fact]
        public void ClearingSelection_KeepsFormFields()
        {
            ConnectionProfile saved = new() { Name = "Local", Server = "localhost" };
            ConnectionDialogViewModel viewModel = CreateViewModel(saved);
            viewModel.SelectedSavedConnection = saved;

            viewModel.SelectedSavedConnection = null;

            Assert.Equal("localhost", viewModel.Server);
        }

        [Fact]
        public void Accept_AfterSelectingRawConnection_ReturnsSavedConnectionString()
        {
            const string connectionString = "Data Source=localhost;Initial Catalog=master;Integrated Security=True";

            ConnectionProfile saved = new()
            {
                Name = "local",
                UseRawConnectionString = true,
                RawConnectionString = connectionString
            };

            ConnectionDialogViewModel viewModel = CreateViewModel(saved);

            viewModel.SelectedSavedConnection = saved;
            bool accepted = viewModel.Accept();

            Assert.True(accepted);
            Assert.NotNull(viewModel.Result);
            Assert.True(viewModel.Result!.UseRawConnectionString);
            Assert.Equal(connectionString, viewModel.Result.RawConnectionString);
            Assert.Equal("local", viewModel.Result.Name);
        }

        [Fact]
        public void Accept_WithoutSelection_FailsValidation()
        {
            ConnectionDialogViewModel viewModel = CreateViewModel(new ConnectionProfile { Name = "Local", Server = "localhost" });

            bool accepted = viewModel.Accept();

            Assert.False(accepted);
            Assert.Null(viewModel.Result);
            Assert.Equal("Enter a server name.", viewModel.StatusMessage);
        }

        [Fact]
        public void New_ResetsFormFieldsToDefaults()
        {
            ConnectionProfile saved = new()
            {
                Name = "Prod",
                Server = "sql01",
                AuthenticationType = AuthenticationType.SqlServer,
                UserId = "app",
                Database = "Sales",
                Encrypt = false,
                TrustServerCertificate = false,
                SavePassword = true,
                Password = "secret"
            };

            ConnectionDialogViewModel viewModel = CreateViewModel(saved);
            viewModel.SelectedSavedConnection = saved;

            viewModel.NewCommand.Execute(null);

            Assert.Equal(string.Empty, viewModel.Name);
            Assert.False(viewModel.UseRawConnectionString);
            Assert.Equal(string.Empty, viewModel.Server);
            Assert.Equal(AuthenticationType.Windows, viewModel.AuthenticationType);
            Assert.Equal(string.Empty, viewModel.UserId);
            Assert.Equal(string.Empty, viewModel.Database);
            Assert.True(viewModel.Encrypt);
            Assert.True(viewModel.TrustServerCertificate);
            Assert.Equal(string.Empty, viewModel.RawConnectionString);
            Assert.False(viewModel.SaveConnection);
            Assert.False(viewModel.SavePassword);
            Assert.Null(viewModel.Password);
        }

        [Fact]
        public void New_DeselectsSavedConnection()
        {
            ConnectionProfile saved = new() { Name = "Local", Server = "localhost" };
            ConnectionDialogViewModel viewModel = CreateViewModel(saved);
            viewModel.SelectedSavedConnection = saved;

            viewModel.NewCommand.Execute(null);

            Assert.Null(viewModel.SelectedSavedConnection);
        }

        [Fact]
        public void New_RaisesFormReset()
        {
            ConnectionProfile saved = new() { Name = "Local", Server = "localhost" };
            ConnectionDialogViewModel viewModel = CreateViewModel(saved);
            viewModel.SelectedSavedConnection = saved;
            int raised = 0;
            viewModel.FormReset += (sender, e) => raised++;

            viewModel.NewCommand.Execute(null);

            Assert.Equal(1, raised);
        }

        [Fact]
        public void DeleteSelected_RemovesConnectionFromListAndStore()
        {
            ConnectionProfile saved = new() { Name = "Local", Server = "localhost" };
            FakeConnectionStore store = new(saved);
            ConnectionDialogViewModel viewModel = new(new FakeSqlConnectionService(), store);
            viewModel.SelectedSavedConnection = saved;

            viewModel.DeleteSelectedCommand.Execute(null);

            Assert.Empty(viewModel.SavedConnections);
            Assert.Empty(store.Profiles);
        }

        /// <summary>
        /// Creates a dialog view model over an in-memory store holding the profiles.
        /// </summary>
        /// <param name="profiles">The saved connections.</param>
        /// <returns>The view model.</returns>
        private static ConnectionDialogViewModel CreateViewModel(params ConnectionProfile[] profiles)
        {
            return new ConnectionDialogViewModel(new FakeSqlConnectionService(), new FakeConnectionStore(profiles));
        }
    }
}