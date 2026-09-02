using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Helpers;
using Xunit;

namespace AowEmailWrapper.Tests
{
    public class CryptographyTests
    {
        [Fact]
        public void ProtectedPasswordRoundTripsAndHidesTheValue()
        {
            string stored = CryptographyHelper.ProtectPassword("s3cret!");

            Assert.True(CryptographyHelper.IsProtected(stored));
            Assert.DoesNotContain("s3cret", stored);
            Assert.Equal("s3cret!", CryptographyHelper.UnprotectPassword(stored));
        }

        [Fact]
        public void LegacyObfuscatedPasswordIsStillReadable()
        {
            string legacy = CryptographyHelper.Obfuscate("old-pass");

            Assert.False(CryptographyHelper.IsProtected(legacy));
            Assert.Equal("old-pass", CryptographyHelper.UnprotectPassword(legacy));
        }

        [Fact]
        public void EmptyAndCorruptValuesYieldEmptyWithoutThrowing()
        {
            Assert.Equal("", CryptographyHelper.UnprotectPassword(""));
            Assert.Equal("", CryptographyHelper.UnprotectPassword(null));
            Assert.Equal("", CryptographyHelper.UnprotectPassword("dpapi:not-base64!!"));
            Assert.Equal("", CryptographyHelper.ProtectPassword(""));
        }

        [Fact]
        public void ConfigValuesStorePasswordsProtected()
        {
            PollingConfigValues polling = new PollingConfigValues();
            polling.PasswordTrue = "pw1";
            Assert.True(CryptographyHelper.IsProtected(polling.Password));
            Assert.Equal("pw1", polling.PasswordTrue);

            SmtpConfigValues smtp = new SmtpConfigValues();
            smtp.PasswordTrue = "pw2";
            Assert.True(CryptographyHelper.IsProtected(smtp.Password));
            Assert.Equal("pw2", smtp.PasswordTrue);
        }

        [Fact]
        public void ObfuscationRoundTripsTurnLogText()
        {
            string text = "Turn 12: Dave to Fred\r\nLine two";
            Assert.Equal(text, CryptographyHelper.Deobfuscate(CryptographyHelper.Obfuscate(text)));
        }
    }
}
