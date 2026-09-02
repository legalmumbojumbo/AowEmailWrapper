using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Helpers;
using AowEmailWrapper.Pollers;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Xunit;
using Xunit.Abstractions;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// A Fact that only runs when AOW_LIVE_TESTS=1 is set: these talk to real mail servers with the
    /// accounts configured in the current user's %APPDATA%\AowEmailWrapper.
    /// </summary>
    public sealed class LiveFactAttribute : FactAttribute
    {
        public LiveFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("AOW_LIVE_TESTS") != "1")
            {
                Skip = "Set AOW_LIVE_TESTS=1 to run against the accounts configured in %APPDATA%\\AowEmailWrapper";
            }
        }
    }

    public class LiveTests
    {
        private readonly ITestOutputHelper _output;

        public LiveTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static Config LoadRealConfig()
        {
            string path = Path.Combine(TestEnvironment.RealAppData, "AowEmailWrapper", "Config", "config.xml");
            Config config = FileHelper.LoadXmlFile<Config>(path);
            Assert.True(config != null && config.AccountsList != null && config.AccountsList.Accounts.Count > 0, "no accounts configured in " + path);
            return config;
        }

        private static bool CanSignIn(AccountConfigValues account)
        {
            //Microsoft sign-in needs the client id and the token cache of the real application, which the test host does not have
            return !MicrosoftOAuth.IsProvider(account.OAuthProvider) || MicrosoftOAuth.IsConfigured;
        }

        [LiveFact]
        public void EveryPasswordAccountSignsInForIncomingAndOutgoingMail()
        {
            List<string> failures = new List<string>();

            foreach (AccountConfigValues account in LoadRealConfig().AccountsList.Accounts.Where(CanSignIn))
            {
                ConnectionTestResult incoming = ConnectionTester.TestIncoming(account.PollingConfig, account.OAuthProvider);
                ConnectionTestResult outgoing = ConnectionTester.TestOutgoing(account.SmtpConfig, account.PollingConfig, account.OAuthProvider);
                _output.WriteLine("{0}: incoming {1}, outgoing {2}", account.Name,
                    incoming.Success ? "OK" : incoming.Error.Message, outgoing.Success ? "OK" : outgoing.Error.Message);

                if (!incoming.Success) failures.Add(account.Name + " incoming: " + incoming.Error.Message);
                if (!outgoing.Success) failures.Add(account.Name + " outgoing: " + outgoing.Error.Message);
            }

            Assert.Empty(failures);
        }

        [LiveFact]
        public void InboxScanOfTheStartupAccountCompletesQuickly()
        {
            AccountConfigValuesList accounts = LoadRealConfig().AccountsList;
            AccountConfigValues account = accounts.StartUpAccount ?? accounts.Accounts[0];
            Assert.True(CanSignIn(account), "startup account needs Microsoft sign-in, which the test host cannot do");
            Assert.Equal(EmailType.IMAP, account.PollingConfig.EmailType);

            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            using (ImapClient imap = new ImapClient())
            {
                imap.Connect(account.PollingConfig.Server, account.PollingConfig.Port, MailHelper.ToSecureSocketOptions(account.PollingConfig.SSLType));
                MailHelper.Authenticate(imap, account.PollingConfig.Username, account.PollingConfig.PasswordTrue, account.OAuthProvider);
                IMailFolder inbox = imap.Inbox;
                inbox.Open(FolderAccess.ReadOnly);

                IList<UniqueId> unread = inbox.Search(SearchQuery.NotSeen);
                IList<UniqueId> recent = inbox.Search(SearchQuery.NotSeen.And(SearchQuery.DeliveredAfter(DateTime.Now.AddDays(-ImapPoller.LookBackDays))));
                HashSet<long> withSaves = ImapPoller.FindMessagesWithSaveGames(inbox, recent.Select(u => (long)u.Id).ToList());

                _output.WriteLine("{0} unread, {1} within {2} days, {3} with save games, {4} ms",
                    unread.Count, recent.Count, ImapPoller.LookBackDays, withSaves.Count, watch.ElapsedMilliseconds);
                imap.Disconnect(true);
            }

            Assert.True(watch.ElapsedMilliseconds < 120000, "scan took " + watch.ElapsedMilliseconds + " ms");
        }
    }
}
