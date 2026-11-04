using System.Globalization;
using System.IO;
using System.Text;
using LightSQL.Models;
using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests that .sql files are read with the right encoding and saved back
    /// byte-for-byte in the same encoding.
    /// </summary>
    public class FileServiceTests : IDisposable
    {
        private const string SampleSql = "SELECT N'Čačak – Größe' AS Grad;\r\nGO\r\n";

        private readonly string filePath;
        private readonly FileService fileService = new();

        /// <summary>
        /// Uses a unique temporary file per test.
        /// </summary>
        public FileServiceTests()
        {
            filePath = Path.Combine(Path.GetTempPath(), $"lightsql-file-{Guid.NewGuid():N}.sql");
        }

        /// <summary>
        /// Removes the temporary file.
        /// </summary>
        public void Dispose()
        {
            if (File.Exists(filePath) == true)
            {
                File.Delete(filePath);
            }
        }

        [Fact]
        public void ReadText_Utf8WithBom_DetectsBom_AndRoundTripsUnchanged()
        {
            byte[] original = Encode(new UTF8Encoding(true), SampleSql);
            File.WriteAllBytes(filePath, original);

            TextFileContent content = fileService.ReadText(filePath);

            Assert.Equal(SampleSql, content.Text);
            Assert.Equal(65001, content.Encoding.CodePage);
            Assert.NotEmpty(content.Encoding.GetPreamble());
            AssertRoundTrip(content, original);
        }

        [Fact]
        public void ReadText_Utf8WithoutBom_StaysWithoutBom()
        {
            byte[] original = new UTF8Encoding(false).GetBytes(SampleSql);
            File.WriteAllBytes(filePath, original);

            TextFileContent content = fileService.ReadText(filePath);

            Assert.Equal(SampleSql, content.Text);
            Assert.Empty(content.Encoding.GetPreamble());
            AssertRoundTrip(content, original);
        }

        [Fact]
        public void ReadText_Utf16LittleEndian_DetectsEncoding_AndRoundTrips()
        {
            byte[] original = Encode(new UnicodeEncoding(false, true), SampleSql);
            File.WriteAllBytes(filePath, original);

            TextFileContent content = fileService.ReadText(filePath);

            Assert.Equal(SampleSql, content.Text);
            Assert.Equal(1200, content.Encoding.CodePage);
            AssertRoundTrip(content, original);
        }

        [Fact]
        public void ReadText_Utf16BigEndian_DetectsEncoding()
        {
            File.WriteAllBytes(filePath, Encode(new UnicodeEncoding(true, true), SampleSql));

            TextFileContent content = fileService.ReadText(filePath);

            Assert.Equal(SampleSql, content.Text);
            Assert.Equal(1201, content.Encoding.CodePage);
        }

        [Fact]
        public void ReadText_InvalidUtf8_FallsBackToAnsiCodePage_AndRoundTrips()
        {
            // Bytes that are valid in single-byte code pages but not valid UTF-8.
            byte[] original = { 0x53, 0x45, 0x4C, 0x45, 0x43, 0x54, 0x20, 0x27, 0xE9, 0xE8, 0x27 };
            File.WriteAllBytes(filePath, original);

            TextFileContent content = fileService.ReadText(filePath);

            Assert.Equal(CultureInfo.CurrentCulture.TextInfo.ANSICodePage, content.Encoding.CodePage);
            Assert.StartsWith("SELECT '", content.Text);
            AssertRoundTrip(content, original);
        }

        [Fact]
        public void ReadText_EmptyFile_ReturnsEmptyText()
        {
            File.WriteAllBytes(filePath, Array.Empty<byte>());

            TextFileContent content = fileService.ReadText(filePath);

            Assert.Equal(string.Empty, content.Text);
        }

        [Fact]
        public void WriteText_DefaultEncoding_WritesUtf8WithBom()
        {
            fileService.WriteText(filePath, "SELECT 1", fileService.DefaultEncoding);

            byte[] written = File.ReadAllBytes(filePath);

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, written.Take(3));
            Assert.Equal("SELECT 1", fileService.ReadText(filePath).Text);
        }

        [Fact]
        public void ReadText_MissingFile_Throws()
        {
            Assert.Throws<FileNotFoundException>(() => fileService.ReadText(filePath));
        }

        /// <summary>
        /// Encodes text with the encoding's byte order mark in front.
        /// </summary>
        /// <param name="encoding">The encoding.</param>
        /// <param name="text">The text.</param>
        /// <returns>The file bytes.</returns>
        private static byte[] Encode(Encoding encoding, string text)
        {
            return encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        }

        /// <summary>
        /// Saves the content back with its own encoding and checks the file bytes are
        /// identical to the original.
        /// </summary>
        /// <param name="content">The content that was read.</param>
        /// <param name="original">The original file bytes.</param>
        private void AssertRoundTrip(TextFileContent content, byte[] original)
        {
            fileService.WriteText(filePath, content.Text, content.Encoding);

            Assert.Equal(original, File.ReadAllBytes(filePath));
        }
    }
}