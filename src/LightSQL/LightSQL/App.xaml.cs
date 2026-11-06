using System.Windows;
using LightSQL.Services;
using LightSQL.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LightSQL
{
    /// <summary>
    /// Application entry point. Configures dependency injection and shows the main
    /// window.
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? serviceProvider;

        /// <summary>
        /// Builds the service container and shows the main window on startup.
        /// </summary>
        /// <param name="e">The startup event data.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ServiceCollection services = new();
            ConfigureServices(services);

            serviceProvider = services.BuildServiceProvider();

            // Apply the saved theme before any window is created, and keep the
            // Windows-drawn title bar of every window (opened now or later) in step.
            IThemeService themeService = serviceProvider.GetRequiredService<IThemeService>();
            themeService.ApplyTheme(serviceProvider.GetRequiredService<IOptionsService>().Current.Theme);
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((sender, _) => WindowTitleBar.Apply((Window)sender, themeService.IsDark)));

            MainWindow mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            // Open .sql files passed on the command line (e.g. "Open with LightSQL").
            if (e.Args.Length > 0)
            {
                serviceProvider.GetRequiredService<MainViewModel>().OpenFiles(e.Args);
            }
        }

        /// <summary>
        /// Disposes the service container on exit.
        /// </summary>
        /// <param name="e">The exit event data.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            serviceProvider?.Dispose();

            base.OnExit(e);
        }

        /// <summary>
        /// Registers services, view models and windows with the DI container.
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        private static void ConfigureServices(ServiceCollection services)
        {
            // Services (single instances for the application lifetime).
            services.AddSingleton<ICredentialProtector, DpapiCredentialProtector>();
            services.AddSingleton<IOptionsService, OptionsService>();
            services.AddSingleton<IConnectionStore, ConnectionStore>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<ISqlConnectionService, SqlConnectionService>();
            services.AddSingleton<ISqlStatementAnalyzer, SqlStatementAnalyzer>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<ISystemThemeProvider, WindowsSystemThemeProvider>();
            services.AddSingleton<IThemeService, ThemeService>();

            // View models. Query tabs (and their ResultsViewModel) are created by
            // MainViewModel, one per open query, so they are not registered here.
            services.AddSingleton<ObjectExplorerViewModel>();
            services.AddSingleton<MainViewModel>();

            // Dialog view models (a fresh instance each time a dialog is shown).
            services.AddTransient<ConnectionDialogViewModel>();
            services.AddTransient<OptionsViewModel>();

            // Windows.
            services.AddSingleton<MainWindow>();
        }
    }
}
