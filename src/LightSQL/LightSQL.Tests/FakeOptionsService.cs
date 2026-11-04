using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.Tests
{
    /// <summary>
    /// In-memory <see cref="IOptionsService"/> for view-model tests (no disk access).
    /// </summary>
    public sealed class FakeOptionsService : IOptionsService
    {
        /// <inheritdoc />
        public AppOptions Current { get; private set; } = new();

        /// <inheritdoc />
        public AppOptions Load()
        {
            return Current;
        }

        /// <inheritdoc />
        public void Save(AppOptions options)
        {
            Current = options;
        }
    }
}
