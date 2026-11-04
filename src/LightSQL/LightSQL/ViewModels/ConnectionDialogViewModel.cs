using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// Backs the connection dialog: a form or raw connection string, the list of
    /// saved connections, and Test / Save behaviour.
    ///
    /// The plaintext password is intentionally not an observable/bindable property;
    /// it is exchanged with the dialog's PasswordBox in code-behind so it is never
    /// held by the binding system.
    /// </summary>
    public partial class ConnectionDialogViewModel : ObservableObject
    {
        private readonly ISqlConnectionService sqlService;
        private readonly IConnectionStore connectionStore;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private bool useRawConnectionString;

        [ObservableProperty]
        private string server = string.Empty;

        [ObservableProperty]
        private AuthenticationType authenticationType = AuthenticationType.Windows;

        [ObservableProperty]
        private string userId = string.Empty;

        [ObservableProperty]
        private string database = string.Empty;

        [ObservableProperty]
        private bool encrypt = true;

        [ObservableProperty]
        private bool trustServerCertificate = true;

        [ObservableProperty]
        private string rawConnectionString = string.Empty;

        [ObservableProperty]
        private bool saveConnection;

        [ObservableProperty]
        private bool savePassword;

        [ObservableProperty]
        private ConnectionProfile? selectedSavedConnection;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        /// <summary>
        /// Creates the view model and loads any saved connections.
        /// </summary>
        /// <param name="sqlService">Service used to test connections.</param>
        /// <param name="connectionStore">Store of saved connections.</param>
        public ConnectionDialogViewModel(ISqlConnectionService sqlService, IConnectionStore connectionStore)
        {
            this.sqlService = sqlService;
            this.connectionStore = connectionStore;

            foreach (ConnectionProfile profile in connectionStore.GetAll())
            {
                SavedConnections.Add(profile);
            }
        }

        /// <summary>
        /// The available authentication types for selection.
        /// </summary>
        public IReadOnlyList<AuthenticationType> AuthenticationTypes { get; } = new[]
        {
            AuthenticationType.Windows,
            AuthenticationType.SqlServer
        };

        /// <summary>
        /// The saved connections available for quick reuse.
        /// </summary>
        public ObservableCollection<ConnectionProfile> SavedConnections { get; } = new();

        /// <summary>
        /// Whether SQL Server authentication is selected (enables user/password fields).
        /// </summary>
        public bool IsSqlServerAuth
        {
            get
            {
                return AuthenticationType == AuthenticationType.SqlServer;
            }
        }

        /// <summary>
        /// The plaintext password, exchanged with the PasswordBox in code-behind.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// The profile chosen when the dialog is accepted, or null.
        /// </summary>
        public ConnectionProfile? Result { get; private set; }

        /// <summary>
        /// Copies the selected saved connection into the form fields. Called
        /// automatically whenever a saved connection is selected in the list.
        /// Raises <see cref="SavedConnectionLoaded"/> so the view can refresh its
        /// password boxes (the password is not bindable).
        /// </summary>
        public void LoadSelected()
        {
            if (SelectedSavedConnection is null)
            {
                return;
            }

            ConnectionProfile profile = SelectedSavedConnection;

            Name = profile.Name;
            UseRawConnectionString = profile.UseRawConnectionString;
            Server = profile.Server;
            AuthenticationType = profile.AuthenticationType;
            UserId = profile.UserId;
            Database = profile.Database;
            Encrypt = profile.Encrypt;
            TrustServerCertificate = profile.TrustServerCertificate;
            RawConnectionString = profile.RawConnectionString;
            SaveConnection = true;
            SavePassword = profile.SavePassword;
            Password = profile.Password;

            StatusMessage = $"Loaded '{profile.Name}'.";

            SavedConnectionLoaded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Deletes the selected saved connection.
        /// </summary>
        [RelayCommand]
        private void DeleteSelected()
        {
            if (SelectedSavedConnection is null)
            {
                return;
            }

            connectionStore.Delete(SelectedSavedConnection.Name);
            SavedConnections.Remove(SelectedSavedConnection);
            StatusMessage = "Connection deleted.";
        }

        /// <summary>
        /// Tests the current connection details without changing the active connection.
        /// </summary>
        /// <returns>A task that completes when the test finishes.</returns>
        [RelayCommand]
        private async Task TestAsync()
        {
            if (Validate(out string error) == false)
            {
                StatusMessage = error;

                return;
            }

            IsBusy = true;
            StatusMessage = "Testing connection...";

            try
            {
                ConnectionProfile profile = BuildProfile();
                string info = await sqlService.TestConnectionAsync(profile, CancellationToken.None);

                StatusMessage = $"Connected successfully to {info}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Test failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Validates the form, builds the profile, optionally saves it, and records
        /// the result.
        /// </summary>
        /// <returns>True if the dialog can close with a valid connection.</returns>
        public bool Accept()
        {
            if (Validate(out string error) == false)
            {
                StatusMessage = error;

                return false;
            }

            ConnectionProfile profile = BuildProfile();

            if (SaveConnection == true)
            {
                connectionStore.Save(profile);
            }

            Result = profile;

            return true;
        }

        /// <summary>
        /// Raises a change for <see cref="IsSqlServerAuth"/> when the auth type changes.
        /// </summary>
        /// <param name="value">The new authentication type.</param>
        partial void OnAuthenticationTypeChanged(AuthenticationType value)
        {
            OnPropertyChanged(nameof(IsSqlServerAuth));
        }

        /// <summary>
        /// Loads a saved connection into the form as soon as it is selected, so the
        /// user can pick a connection and press Connect straight away.
        /// </summary>
        /// <param name="value">The newly selected saved connection, or null.</param>
        partial void OnSelectedSavedConnectionChanged(ConnectionProfile? value)
        {
            if (value is null)
            {
                return;
            }

            LoadSelected();
        }

        /// <summary>
        /// Validates the current form.
        /// </summary>
        /// <param name="error">The validation error, when invalid.</param>
        /// <returns>True when the form is valid.</returns>
        private bool Validate(out string error)
        {
            if (UseRawConnectionString == true)
            {
                if (string.IsNullOrWhiteSpace(RawConnectionString) == true)
                {
                    error = "Enter a connection string.";

                    return false;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Server) == true)
                {
                    error = "Enter a server name.";

                    return false;
                }

                if (IsSqlServerAuth == true && string.IsNullOrWhiteSpace(UserId) == true)
                {
                    error = "Enter a login name for SQL Server authentication.";

                    return false;
                }
            }

            if (SaveConnection == true && string.IsNullOrWhiteSpace(Name) == true)
            {
                error = "Enter a name to save the connection.";

                return false;
            }

            error = string.Empty;

            return true;
        }

        /// <summary>
        /// Builds a connection profile from the current form values.
        /// </summary>
        /// <returns>The populated profile.</returns>
        private ConnectionProfile BuildProfile()
        {
            string effectiveName = string.IsNullOrWhiteSpace(Name) == true
                ? (UseRawConnectionString == true ? "Connection" : Server)
                : Name;

            return new ConnectionProfile
            {
                Name = effectiveName,
                UseRawConnectionString = UseRawConnectionString,
                Server = Server,
                AuthenticationType = AuthenticationType,
                UserId = UserId,
                Database = Database,
                Encrypt = Encrypt,
                TrustServerCertificate = TrustServerCertificate,
                RawConnectionString = RawConnectionString,
                SavePassword = SavePassword,
                Password = Password
            };
        }

        /// <summary>
        /// Raised after a saved connection has been copied into the form, so the
        /// view can copy <see cref="Password"/> into its password boxes.
        /// </summary>
        public event EventHandler? SavedConnectionLoaded;
    }
}
