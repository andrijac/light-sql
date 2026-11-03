using System.Security.Cryptography;
using System.Text;

namespace LightSQL.Services
{
    /// <summary>
    /// Protects secrets using the Windows Data Protection API (DPAPI) scoped to the
    /// current user. Encrypted values can only be decrypted by the same Windows user
    /// on the same machine, so saved passwords never leave the user's profile in a
    /// readable form.
    /// </summary>
    public sealed class DpapiCredentialProtector : ICredentialProtector
    {
        /// <summary>
        /// Extra entropy mixed into the encryption so that a value protected by this
        /// application cannot be trivially decrypted by another application.
        /// </summary>
        private static readonly byte[] entropy = Encoding.UTF8.GetBytes("LightSQL.ConnectionCredential.v1");

        /// <inheritdoc />
        public string Protect(string plaintext)
        {
            if (plaintext is null)
            {
                throw new ArgumentNullException(nameof(plaintext));
            }

            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] encrypted = ProtectedData.Protect(plaintextBytes, entropy, DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encrypted);
        }

        /// <inheritdoc />
        public string? Unprotect(string protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue) == true)
            {
                return null;
            }

            try
            {
                byte[] encrypted = Convert.FromBase64String(protectedValue);
                byte[] plaintextBytes = ProtectedData.Unprotect(encrypted, entropy, DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(plaintextBytes);
            }
            catch (Exception)
            {
                // A tampered, corrupt, or cross-user value cannot be decrypted; treat as absent.
                return null;
            }
        }
    }
}
