using System.Text;
using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// Reads and writes the SQL script files opened in query tabs.
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        /// The encoding used for files that are saved for the first time.
        /// </summary>
        Encoding DefaultEncoding { get; }

        /// <summary>
        /// Reads a text file, detecting its encoding.
        /// </summary>
        /// <param name="path">The full path of the file.</param>
        /// <returns>The text and the detected encoding.</returns>
        TextFileContent ReadText(string path);

        /// <summary>
        /// Writes text to a file, replacing any existing content.
        /// </summary>
        /// <param name="path">The full path of the file.</param>
        /// <param name="text">The text to write.</param>
        /// <param name="encoding">The encoding to write with.</param>
        void WriteText(string path, string text, Encoding encoding);
    }
}