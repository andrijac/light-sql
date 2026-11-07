using System.IO;
using System.Reflection;

namespace LightSQL.Services
{
    /// <summary>
    /// Read-only access to the text prompts embedded in the application.
    /// </summary>
    public static class PromptLibrary
    {
        /// <summary>
        /// Manifest resource name of the database-metadata extraction prompt.
        /// </summary>
        private const string MetadataExtractionResource = "LightSQL.Resources.MetadataExtractionPrompt.txt";

        private static string? metadataExtractionPrompt;

        /// <summary>
        /// The database-metadata extraction prompt: an AI agent runs it against a code
        /// repository to produce the JSON pasted into the Database Metadata dialog.
        /// </summary>
        public static string MetadataExtractionPrompt
        {
            get
            {
                metadataExtractionPrompt ??= ReadResource(MetadataExtractionResource);

                return metadataExtractionPrompt;
            }
        }

        /// <summary>
        /// Reads an embedded text resource from this assembly.
        /// </summary>
        /// <param name="resourceName">The full manifest resource name.</param>
        /// <returns>The resource text.</returns>
        private static string ReadResource(string resourceName)
        {
            Assembly assembly = typeof(PromptLibrary).Assembly;

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);

            if (stream is null)
            {
                throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");
            }

            using StreamReader reader = new(stream);

            return reader.ReadToEnd();
        }
    }
}
