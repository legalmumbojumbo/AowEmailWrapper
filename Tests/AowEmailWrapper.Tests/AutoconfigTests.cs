using System.IO;
using System.Xml.Serialization;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Helpers;
using Mozilla.Autoconfig;
using Xunit;

namespace AowEmailWrapper.Tests
{
    public class AutoconfigTests
    {
        /// <summary>The shape of the Thunderbird database entry for Gmail, plus an unknown method for good measure.</summary>
        private const string GmailLike =
            "<clientConfig version=\"1.1\"><emailProvider id=\"googlemail.com\"><domain>gmail.com</domain><displayName>Google Mail</displayName>" +
            "<incomingServer type=\"imap\"><hostname>imap.gmail.com</hostname><port>993</port><socketType>SSL</socketType><username>%EMAILADDRESS%</username>" +
            "<authentication>OAuth2</authentication><authentication>password-cleartext</authentication></incomingServer>" +
            "<outgoingServer type=\"smtp\"><hostname>smtp.gmail.com</hostname><port>465</port><socketType>SSL</socketType><username>%EMAILADDRESS%</username>" +
            "<authentication>OAuth2</authentication></outgoingServer>" +
            "<outgoingServer type=\"smtp\"><hostname>smtp2.example.com</hostname><port>25</port><socketType>plain</socketType>" +
            "<authentication>something-new</authentication></outgoingServer>" +
            "</emailProvider><oAuth2><issuer>accounts.google.com</issuer></oAuth2><enable visiturl=\"x\"><instruction>y</instruction></enable></clientConfig>";

        private static ClientConfig Parse(string xml)
        {
            using (StringReader reader = new StringReader(xml))
            {
                return (ClientConfig)new XmlSerializer(typeof(ClientConfig)).Deserialize(reader);
            }
        }

        [Fact]
        public void GmailShapedEntryDeserialises()
        {
            EmailProvider provider = Parse(GmailLike).EmailProvider;
            Assert.Single(provider.IncomingServers);
            Assert.Equal(2, provider.OutgoingServers.Count);
        }

        [Fact]
        public void PasswordMethodIsPreferredOverOAuth2()
        {
            IncomingServer imap = Parse(GmailLike).EmailProvider.IncomingServers[0];
            Assert.Equal(AuthenticationType.PasswordClearText, imap.Authentication);
            Assert.False(imap.IsOAuthOnly);
        }

        [Fact]
        public void OAuthOnlyServerIsDetected()
        {
            Assert.True(Parse(GmailLike).EmailProvider.OutgoingServers[0].IsOAuthOnly);
        }

        [Fact]
        public void UnknownAuthenticationMethodIsTolerated()
        {
            Assert.Equal(AuthenticationType.Unknown, Parse(GmailLike).EmailProvider.OutgoingServers[1].Authentication);
        }

        [Fact]
        public void UsernameTemplateIsExpanded()
        {
            Assert.Equal("me@gmail.com", Parse(GmailLike).EmailProvider.IncomingServers[0].GetUsernameFormatted("me@gmail.com"));
        }

        [Fact]
        public void MappingProducesAGmailAccountAndFlagsOAuthOnlyServers()
        {
            MechanismResponse response = new MechanismResponse();
            response.ClientConfig = Parse(GmailLike);
            response.ResponseType = MechanismResponseType.Success;

            AccountConfigValues account = AutoconfigurationHelper.MapMechanismResponse(response, "me@gmail.com", "app-pass", ServerType.Unknown);

            Assert.NotNull(account);
            Assert.Equal("imap.gmail.com", account.PollingConfig.Server);
            Assert.Equal(SSLType.SSL, account.PollingConfig.SSLType);
            Assert.Equal("smtp.gmail.com", account.SmtpConfig.SmtpServer);
            Assert.True(account.SmtpConfig.UsePollingCredentials);
            Assert.Equal("app-pass", account.PollingConfig.PasswordTrue);
            //The chosen outgoing server (highest port) only offers OAuth2
            Assert.True(account.RequiresOAuth);
        }

        [Fact]
        public void AccountXmlKeepsTheOAuthProvider()
        {
            AccountConfigValues account = new AccountConfigValues();
            account.Name = "Outlook";
            account.OAuthProvider = MicrosoftOAuth.ProviderName;
            account.PollingConfig = new PollingConfigValues();
            account.SmtpConfig = new SmtpConfigValues();

            string xml;
            using (StringWriter writer = new StringWriter())
            {
                new XmlSerializer(typeof(AccountConfigValues)).Serialize(writer, account);
                xml = writer.ToString();
            }

            Assert.Contains("oauthprovider=\"Microsoft\"", xml);

            using (StringReader reader = new StringReader(xml))
            {
                AccountConfigValues back = (AccountConfigValues)new XmlSerializer(typeof(AccountConfigValues)).Deserialize(reader);
                Assert.True(MicrosoftOAuth.IsProvider(back.OAuthProvider));
            }
        }
    }
}
