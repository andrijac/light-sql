using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightSQL.Models;
using LightSQL.Services;
using LightSQL.Services.Database;

namespace LightSQL.ViewModels
{
    /// <summary>
    /// Backs the connection dialog: the database engine, a form or raw connection string,
    /// the list of saved connections, and Test / Save behaviour.
    ///
    /// The plaintext password is intentionally not an observable/bindable property;
    /// it is exchanged with the dialog's PasswordBox in code-behind so it is never
    /// held by the binding system.
    /// </summary>
    public partial class ConnectionDialogViewModel : ObservableObject
    {
        private readonly ISqlConnectionService sqlService;
        private readonly IConnectionStore connectionStore;
        private readonly IDatabaseProviderRegistry providerRegistry;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SupportsWindowsAuthentication))]
        [NotifyPropertyChangedFor(nameof(ShowAuthenticationType))]
        [NotifyPropertyChangedFor(nameof(AreCredentialsEnabled))]
        private IDatabaseProvider selectedEngine;

        [ObservableProperty]
        private bool useRawConnectionString;

        [ObservableProperty]
        private string server = string.Empty;

        [ObservableProperty]
        private string port = string.Empty;

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
        /// <param name="providerRegistry">The available database engines.</param>
        public ConnectionDialogViewModel(ISqlConnectionService sqlService, IConnectionStore connectionStore, IDatabaseProviderRegistry providerRegistry)
        {
            this.sqlService = sqlService;
            this.connectionStore = connectionStore;
            this.providerRegistry = providerRegistry;

            foreach (IDatabaseProvider provider in providerRegistry.Providers)
            {
                Engines.Add(provider);
            }

            // Default to SQL Server (the first provider), matching ConnectionProfile's default.
            selectedEngine = providerRegistry.Get(DatabaseEngine.SqlServer);

            foreach (ConnectionProfile profile in connectionStore.GetAll())
            {
                SavedConnections.Add(profile);
            }
        }

        /// <summary>
        /// The database engines that can be selected.
        /// </summary>
        public ObservableCollection<IDatabaseProvider> Engines { get; } = new();

        /// <summary>
        /// The available authentication types for selection (SQL Server only).
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
        /// Whether the selected engine supports Windows / integrated authentication
        /// (SQL Server only). Drives whether the authentication-type picker is shown.
        /// </summary>
        public bool SupportsWindowsAuthentication
        {
            get
            {
                return SelectedEngine?.SupportsWindowsAuthentication == true;
            }
        }

        /// <summary>
        /// Whether to show the authentication-type picker (only for engines that support
        /// Windows authentication, i.e. SQL Server).
        /// </summary>
        public bool ShowAuthenticationType
        {
            get
            {
                return SupportsWindowsAuthentication == true;
            }
        }

        /// <summary>
        /// Whether the login and password fields are usable: always for engines without
        /// Windows authentication (PostgreSQL / MySQL), and only under SQL Server
        /// authentication for SQL Server.
        /// </summary>
        public bool AreCredentialsEnabled
        {
            get
            {
                if (SupportsWindowsAuthentication == false)
                {
                    return true;
                }

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
            SelectedEngine = providerRegistry.Get(profile.Engine);
            UseRawConnectionString = profile.UseRawConnectionString;
            Server = profile.Server;
            Port = profile.Port?.ToString() ?? string.Empty;
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
        /// Clears the form back to its empty, default state so the user can define
        /// a brand-new connection instead of editing the selected saved one.
        /// Deselects any saved connection and raises <see cref="FormReset"/> so the
        /// view can clear its password boxes (the password is not bindable).
        /// </summary>
        [RelayCommand]
        private void New()
        {
            // Deselecting first stops the list from reloading the old values.
            SelectedSavedConnection = null;

            Name = string.Empty;
            SelectedEngine = providerRegistry.Get(DatabaseEngine.SqlServer);
            UseRawConnectionString = false;
            Server = string.Empty;
            Port = string.Empty;
            AuthenticationType = AuthenticationType.Windows;
            UserId = string.Empty;
            Database = string.Empty;
            Encrypt = true;
            TrustServerCertificate = true;
            RawConnectionString = string.Empty;
            SaveConnection = false;
            SavePassword = false;
            Password = null;

            StatusMessage = "New connection.";

            FormReset?.Invoke(this, EventArgs.Empty);
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
        /// Raises changes for the credential-related flags when the auth type changes.
        /// </summary>
        /// <param name="value">The new authentication type.</param>
        partial void OnAuthenticationTypeChanged(AuthenticationType value)
        {
            OnPropertyChanged(nameof(AreCredentialsEnabled));
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

                if (string.IsNullOrWhiteSpace(Port) == false && int.TryParse(Port, out int _) == false)
                {
                    error = "Port must be a number.";

                    return false;
                }

                if (AreCredentialsEnabled == true && string.IsNullOrWhiteSpace(UserId) == true)
                {
                    error = "Enter a login name.";

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

            int? parsedPort = int.TryParse(Port, out int portValue) == true ? portValue : null;

            return new ConnectionProfile
            {
                Name = effectiveName,
                Engine = SelectedEngine.Engine,
                UseRawConnectionString = UseRawConnectionString,
                Server = Server,
                Port = parsedPort,
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

        /// <summary>
        /// Raised after the form has been reset to its empty state, so the view can
        /// clear its password boxes (the password is not bindable).
        /// </summary>
        public event EventHandler? FormReset;
    }
}
