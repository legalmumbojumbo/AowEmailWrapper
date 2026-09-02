using AowEmailWrapper.Helpers;
using Xunit;

namespace AowEmailWrapper.Tests
{
    public class ProviderHintsTests
    {
        [Theory]
        [InlineData("someone@gmail.com", ProviderHints.GmailMessageKey)]
        [InlineData("someone@googlemail.com", ProviderHints.GmailMessageKey)]
        [InlineData("someone@yahoo.co.uk", ProviderHints.YahooMessageKey)]
        [InlineData("someone@aol.com", ProviderHints.YahooMessageKey)]
        [InlineData("someone@hotmail.com", ProviderHints.MicrosoftMessageKey)]
        [InlineData("someone@outlook.de", ProviderHints.MicrosoftMessageKey)]
        [InlineData("someone@live.com", ProviderHints.MicrosoftMessageKey)]
        public void KnownProvidersGetTheirHint(string address, string expectedKey)
        {
            ProviderHint hint = ProviderHints.ForEmailAddress(address);
            Assert.NotNull(hint);
            Assert.Equal(expectedKey, hint.MessageKey);
            Assert.Equal(expectedKey + "Short", hint.ShortMessageKey);
        }

        [Theory]
        [InlineData("someone@example.org")]
        [InlineData("notanaddress")]
        [InlineData("")]
        [InlineData(null)]
        public void UnknownOrMalformedAddressesGetNoHint(string address)
        {
            Assert.Null(ProviderHints.ForEmailAddress(address));
        }

        [Theory]
        [InlineData("smtp.gmail.com", ProviderHints.GmailMessageKey)]
        [InlineData("imap.gmail.com", ProviderHints.GmailMessageKey)]
        [InlineData("outlook.office365.com", ProviderHints.MicrosoftMessageKey)]
        [InlineData("smtp-mail.outlook.com", ProviderHints.MicrosoftMessageKey)]
        [InlineData("imap.mail.yahoo.com", ProviderHints.YahooMessageKey)]
        public void KnownHostsGetTheirHint(string host, string expectedKey)
        {
            Assert.Equal(expectedKey, ProviderHints.ForHost(host).MessageKey);
        }

        [Fact]
        public void UnknownHostGetsNoHint()
        {
            Assert.Null(ProviderHints.ForHost("mail.example.org"));
        }

        [Fact]
        public void GmailAndYahooHintsLinkToAnAppPasswordPage()
        {
            Assert.StartsWith("https://", ProviderHints.ForEmailAddress("a@gmail.com").Url);
            Assert.StartsWith("https://", ProviderHints.ForEmailAddress("a@yahoo.com").Url);
            Assert.Null(ProviderHints.ForEmailAddress("a@hotmail.com").Url);
        }
    }
}
