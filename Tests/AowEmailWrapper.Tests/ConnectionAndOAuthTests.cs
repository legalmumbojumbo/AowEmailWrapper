using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Helpers;
using MailKit.Net.Imap;
using MailKit.Security;
using Xunit;

namespace AowEmailWrapper.Tests
{
    public class ConnectionAndOAuthTests
    {
        [Fact]
        public void ConnectionTestReportsAConnectFailureCleanly()
        {
            PollingConfigValues closed = new PollingConfigValues();
            closed.EmailType = EmailType.IMAP;
            closed.Server = "127.0.0.1";
            closed.Port = RelayFixture.FreePort();
            closed.SSLType = SSLType.None;
            closed.Username = "u";
            closed.PasswordTrue = "p";

            ConnectionTestResult result = ConnectionTester.TestIncoming(closed, null);

            Assert.False(result.Success);
            Assert.False(result.AuthenticationFailed);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public void OutgoingTestWithoutAuthenticationSkipsSignIn()
        {
            SmtpConfigValues smtp = new SmtpConfigValues();
            smtp.SmtpServer = "127.0.0.1";
            smtp.Port = RelayFixture.FreePort();
            smtp.SmtpSSLType = SSLType.None;
            smtp.Authentication = false;

            ConnectionTestResult result = ConnectionTester.TestOutgoing(smtp, null, null);

            Assert.False(result.Success);
            Assert.Null(result.Username);
        }

        [Fact]
        public void ProviderNameMatchingIsCaseInsensitiveAndNullSafe()
        {
            Assert.True(MicrosoftOAuth.IsProvider("microsoft"));
            Assert.True(MicrosoftOAuth.IsProvider("Microsoft"));
            Assert.False(MicrosoftOAuth.IsProvider(null));
            Assert.False(MicrosoftOAuth.IsProvider(""));
            Assert.False(MicrosoftOAuth.IsProvider("Google"));
        }

        [Fact]
        public void UnconfiguredMicrosoftSignInFailsAsAnAuthenticationProblem()
        {
            //The test host has no client id in its configuration
            Assert.False(MicrosoftOAuth.IsConfigured);

            using (ImapClient imap = new ImapClient())
            {
                AuthenticationException ex = Assert.Throws<AuthenticationException>(
                    () => MailHelper.Authenticate(imap, "someone@outlook.com", "", MicrosoftOAuth.ProviderName));
                Assert.Contains("not set up", ex.Message);
            }
        }

        [Theory]
        [InlineData(SSLType.SSL, SecureSocketOptions.SslOnConnect)]
        [InlineData(SSLType.TLS, SecureSocketOptions.StartTls)]
        [InlineData(SSLType.None, SecureSocketOptions.Auto)]
        public void SslTypeMapsToMailKitOptions(SSLType sslType, SecureSocketOptions expected)
        {
            Assert.Equal(expected, MailHelper.ToSecureSocketOptions(sslType));
        }
    }
}
