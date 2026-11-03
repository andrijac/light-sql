namespace LightSQL.Services
{
    /// <summary>
    /// Encrypts and decrypts sensitive strings (such as SQL passwords) so they are
    /// never persisted in plaintext.
    /// </summary>
    public interface ICredentialProtector
    {
        /// <summary>
        /// Encrypts a plaintext value.
        /// </summary>
        /// <param name="plaintext">The value to protect.</param>
        /// <returns>An opaque, encrypted representation safe to persist.</returns>
        string Protect(string plaintext);

        /// <summary>
        /// Decrypts a value previously produced by <see cref="Protect"/>.
        /// </summary>
        /// <param name="protectedValue">The encrypted value.</param>
        /// <returns>The original plaintext, or null if decryption fails.</returns>
        string? Unprotect(string protectedValue);
    }
}
