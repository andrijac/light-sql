using System.Globalization;
using System.IO;
using System.Text;
using LightSQL.Models;

namespace LightSQL.Services
{
    /// <summary>
    /// File-system implementation of <see cref="IFileService"/>.
    ///
    /// Encoding handling, so files round-trip unchanged:
    /// <list type="bullet">
    /// <item>A byte order mark (UTF-8, UTF-16, UTF-32) decides the encoding.</item>
    /// <item>Without one, the file is read as UTF-8 when it is valid UTF-8, otherwise
    /// in the system's ANSI code page (older SSMS scripts are often ANSI).</item>
    /// <item>A file is saved in the encoding it was read with; new files use UTF-8
    /// with a byte order mark, which SSMS also uses.</item>
    /// </list>
    /// </summary>
    public sealed class FileService : IFileService
    {
        /// <summary>
        /// Strict UTF-8 without a byte order mark: throws on invalid bytes so ANSI
        /// files can be detected.
        /// </summary>
        private static readonly UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <inheritdoc />
        public Encoding DefaultEncoding
        {
            get
            {
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            }
        }

        /// <summary>
        /// Registers the code-page encodings (e.g. Windows-1252) that .NET does not
        /// provide by default.
        /// </summary>
        static FileService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <inheritdoc />
        public TextFileContent ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Encoding? bomEncoding = DetectByteOrderMark(bytes, out int preambleLength);

            if (bomEncoding is not null)
            {
                string text = bomEncoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);

                return new TextFileContent(text, bomEncoding);
            }

            try
            {
                return new TextFileContent(strictUtf8.GetString(bytes), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (DecoderFallbackException)
            {
                Encoding ansi = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);

                return new TextFileContent(ansi.GetString(bytes), ansi);
            }
        }

        /// <inheritdoc />
        public void WriteText(string path, string text, Encoding encoding)
        {
            // File.WriteAllText writes the encoding's byte order mark when it has one.
            File.WriteAllText(path, text, encoding);
        }

        /// <summary>
        /// Detects a Unicode byte order mark at the start of the data.
        /// </summary>
        /// <param name="bytes">The file content.</param>
        /// <param name="preambleLength">The length of the byte order mark, or 0.</param>
        /// <returns>The encoding the mark stands for, or null when there is none.</returns>
        private static Encoding? DetectByteOrderMark(byte[] bytes, out int preambleLength)
        {
            // UTF-32 LE must be checked before UTF-16 LE: both start with FF FE.
            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            {
                preambleLength = 4;

                return new UTF32Encoding(bigEndian: false, byteOrderMark: true);
            }

            if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                preambleLength = 4;

                return new UTF32Encoding(bigEndian: true, byteOrderMark: true);
            }

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                preambleLength = 3;

                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                preambleLength = 2;

                return new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                preambleLength = 2;

                return new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
            }

            preambleLength = 0;

            return null;
        }
    }
}