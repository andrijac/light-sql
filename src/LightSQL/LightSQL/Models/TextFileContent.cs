using System.Text;

namespace LightSQL.Models
{
    /// <summary>
    /// The text of a file together with the encoding it was stored in, so the file
    /// can be saved back in the same encoding.
    /// </summary>
    public sealed class TextFileContent
    {
        /// <summary>
        /// Creates the file content.
        /// </summary>
        /// <param name="text">The decoded text.</param>
        /// <param name="encoding">The encoding the file was stored in.</param>
        public TextFileContent(string text, Encoding encoding)
        {
            Text = text;
            Encoding = encoding;
        }

        /// <summary>
        /// The decoded text.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// The encoding the file was stored in (including whether it had a byte order mark).
        /// </summary>
        public Encoding Encoding { get; }
    }
}