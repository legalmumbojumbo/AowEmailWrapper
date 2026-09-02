using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Games;
using Xunit;

namespace AowEmailWrapper.Tests
{
    public class AccountModelTests
    {
        private static AccountConfigValues Account(string name, bool checksForEmail)
        {
            AccountConfigValues account = new AccountConfigValues();
            account.Name = name;
            account.PollingConfig = new PollingConfigValues();
            account.PollingConfig.UsePolling = checksForEmail;
            account.SmtpConfig = new SmtpConfigValues();
            account.SmtpConfig.EmailAddress = name.ToLowerInvariant() + "@example.com";
            return account;
        }

        [Fact]
        public void AnAccountIsActiveWhenItChecksForEmail()
        {
            Assert.True(Account("A", true).IsActive);
            Assert.False(Account("B", false).IsActive);
            Assert.False(new AccountConfigValues().IsActive);
        }

        [Fact]
        public void ActiveAccountsAreAllThatCheckForEmail()
        {
            AccountConfigValuesList list = new AccountConfigValuesList();
            list.Accounts = new List<AccountConfigValues> { Account("Gmail", true), Account("Old", false), Account("Outlook", true) };

            Assert.Equal(new[] { "Gmail", "Outlook" }, list.ActiveAccounts.ConvertAll(a => a.Name).ToArray());
        }

        [Fact]
        public void PrimaryAccountIsTheFirstActiveOne()
        {
            AccountConfigValuesList list = new AccountConfigValuesList();
            list.Accounts = new List<AccountConfigValues> { Account("Old", false), Account("Gmail", true), Account("Outlook", true) };

            Assert.Equal("Gmail", list.PrimaryAccount.Name);
        }

        [Fact]
        public void PrimaryAccountFallsBackToTheFirstAccountWhenNoneIsActive()
        {
            AccountConfigValuesList list = new AccountConfigValuesList();
            list.Accounts = new List<AccountConfigValues> { Account("Only", false) };

            Assert.Equal("Only", list.PrimaryAccount.Name);
            Assert.Empty(list.ActiveAccounts);
        }

        [Fact]
        public void EmptyListHasNoPrimaryAccount()
        {
            AccountConfigValuesList list = new AccountConfigValuesList();
            list.Accounts = new List<AccountConfigValues>();

            Assert.Null(list.PrimaryAccount);
        }

        [Fact]
        public void ReceivedTurnRemembersTheAccountItArrivedOn()
        {
            AowGameSavedEventArgs e = new AowGameSavedEventArgs(AowGameType.Aow1, "Highpass.asg", "Highpass", "Highpass", "3");
            e.AccountName = "Outlook";

            Activity activity = new Activity(e);

            Assert.Equal(ActivityState.Received, activity.Status);
            Assert.Equal("Outlook", activity.AccountName);
        }

        [Fact]
        public void ActivityAccountSurvivesTheActivityLogXml()
        {
            Activity activity = new Activity(ActivityState.Sent, AowGameType.Aow1, "Highpass.asg", "Highpass", "3");
            activity.AccountName = "Gmail";

            XmlSerializer serializer = new XmlSerializer(typeof(Activity));
            string xml;
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, activity);
                xml = writer.ToString();
            }

            Assert.Contains("account=\"Gmail\"", xml);

            using (StringReader reader = new StringReader(xml))
            {
                Activity back = (Activity)serializer.Deserialize(reader);
                Assert.Equal("Gmail", back.AccountName);
            }
        }

        [Fact]
        public void OlderActivityXmlWithoutAnAccountStillLoads()
        {
            const string xml = "<activity game_type=\"Aow1\" file_name=\"Highpass.asg\" map_title=\"Highpass\" turn=\"3\" status=\"Sent\" ticks=\"1\" />";
            using (StringReader reader = new StringReader(xml))
            {
                Activity back = (Activity)new XmlSerializer(typeof(Activity)).Deserialize(reader);
                Assert.Null(back.AccountName);
                Assert.Equal("Highpass.asg", back.FileName);
            }
        }
    }
}
