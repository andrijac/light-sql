using System.Text;
using LightSQL.Services;

namespace LightSQL.Tests
{
    /// <summary>
    /// A reversible, test-only credential protector that Base64-encodes values. It
    /// avoids a dependency on real DPAPI while still transforming the plaintext so
    /// tests can assert the original text is not present on disk.
    /// </summary>
    public sealed class FakeCredentialProtector : ICredentialProtector
    {
        /// <inheritdoc />
        public string Protect(string plaintext)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(plaintext);

            return "enc:" + Convert.ToBase64String(bytes);
        }

        /// <inheritdoc />
        public string? Unprotect(string protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue) == true)
            {
                return null;
            }

            const string prefix = "enc:";

            if (protectedValue.StartsWith(prefix, StringComparison.Ordinal) == false)
            {
                return null;
            }

            byte[] bytes = Convert.FromBase64String(protectedValue.Substring(prefix.Length));

            return Encoding.UTF8.GetString(bytes);
        }
    }
}
