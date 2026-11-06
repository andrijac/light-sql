using LightSQL.Models;

namespace LightSQL.Services.Database
{
    /// <summary>
    /// Resolves the <see cref="IDatabaseProvider"/> for a database engine and exposes the
    /// full set of providers (for the connection dialog's engine picker).
    /// </summary>
    public interface IDatabaseProviderRegistry
    {
        /// <summary>
        /// All registered providers, in display order.
        /// </summary>
        IReadOnlyList<IDatabaseProvider> Providers { get; }

        /// <summary>
        /// Returns the provider for an engine.
        /// </summary>
        /// <param name="engine">The database engine.</param>
        /// <returns>The matching provider.</returns>
        /// <exception cref="NotSupportedException">No provider is registered for the engine.</exception>
        IDatabaseProvider Get(DatabaseEngine engine);
    }
}
