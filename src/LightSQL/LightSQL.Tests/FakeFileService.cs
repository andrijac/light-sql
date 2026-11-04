using System.IO;
using System.Text;
using LightSQL.Models;
using LightSQL.Services;

namespace LightSQL.Tests
{
    /// <summary>
    /// In-memory <see cref="IFileService"/> for view-model tests: files are entries in
    /// a dictionary keyed by full path (case-insensitive, like Windows).
    /// </summary>
    public sealed class FakeFileService : IFileService
    {
        /// <summary>
        /// The "files on disk": path → text and encoding.
        /// </summary>
        public Dictionary<string, TextFileContent> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// When set, <see cref="WriteText"/> throws this exception (e.g. access denied).
        /// </summary>
        public Exception? WriteFailure { get; set; }

        /// <inheritdoc />
        public Encoding DefaultEncoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        /// <summary>
        /// Adds a file to the fake disk.
        /// </summary>
        /// <param name="path">The full path.</param>
        /// <param name="text">The file text.</param>
        /// <param name="encoding">The file encoding, or null for UTF-8.</param>
        public void AddFile(string path, string text, Encoding? encoding = null)
        {
            Files[path] = new TextFileContent(text, encoding ?? new UTF8Encoding(false));
        }

        /// <inheritdoc />
        public TextFileContent ReadText(string path)
        {
            if (Files.TryGetValue(path, out TextFileContent? content) == false)
            {
                throw new FileNotFoundException($"Could not find file '{path}'.", path);
            }

            return content;
        }

        /// <inheritdoc />
        public void WriteText(string path, string text, Encoding encoding)
        {
            if (WriteFailure is not null)
            {
                throw WriteFailure;
            }

            Files[path] = new TextFileContent(text, encoding);
        }
    }
}