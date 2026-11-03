using LightSQL.Services;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests the DPAPI-based credential protector. These run on Windows where DPAPI
    /// is available.
    /// </summary>
    public class DpapiCredentialProtectorTests
    {
        private readonly DpapiCredentialProtector protector = new();

        [Fact]
        public void ProtectThenUnprotect_ReturnsOriginal()
        {
            const string secret = "S3cr3t-Password!";

            string protectedValue = protector.Protect(secret);
            string? recovered = protector.Unprotect(protectedValue);

            Assert.Equal(secret, recovered);
        }

        [Fact]
        public void ProtectedValue_DiffersFromPlaintext()
        {
            const string secret = "PlainTextPassword";

            string protectedValue = protector.Protect(secret);

            Assert.NotEqual(secret, protectedValue);
            Assert.DoesNotContain(secret, protectedValue);
        }

        [Fact]
        public void Unprotect_EmptyValue_ReturnsNull()
        {
            Assert.Null(protector.Unprotect(string.Empty));
        }

        [Fact]
        public void Unprotect_GarbageValue_ReturnsNull()
        {
            Assert.Null(protector.Unprotect("not-valid-base64-or-dpapi"));
        }
    }
}
