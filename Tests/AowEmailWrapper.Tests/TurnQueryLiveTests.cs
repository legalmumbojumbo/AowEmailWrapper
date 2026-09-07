using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AowEmailWrapper.Classes;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Helpers;
using AowEmailWrapper.Pollers;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using Xunit;
using Xunit.Abstractions;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// Against the real IMAP account: a wrapper query put in the inbox is found by its header alone,
    /// and the delete-after-read path really removes it. Cleans up after itself, including anything
    /// an earlier failed run left behind.
    /// </summary>
    public class TurnQueryLiveTests
    {
        private const string TestGame = "Live Test Game.asg";
        private readonly ITestOutputHelper _output;

        public TurnQueryLiveTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [LiveFact]
        public void A_query_in_the_inbox_is_found_by_header_and_removed()
        {
            string path = Path.Combine(TestEnvironment.RealAppData, "AowEmailWrapper", "Config", "config.xml");
            Config config = FileHelper.LoadXmlFile<Config>(path);
            Assert.True(config != null && config.AccountsList != null && config.AccountsList.Accounts.Count > 0, "no accounts configured in " + path);

            AccountConfigValues account = config.AccountsList.Accounts.FirstOrDefault(a =>
                a.PollingConfig != null && a.PollingConfig.EmailType == EmailType.IMAP && !MicrosoftOAuth.IsProvider(a.OAuthProvider));
            Assert.True(account != null, "no password IMAP account to test against");
            PollingConfigValues polling = account.PollingConfig;

            using (ImapClient imap = new ImapClient())
            {
                imap.Connect(polling.Server, polling.Port, MailHelper.ToSecureSocketOptions(polling.SSLType));
                MailHelper.Authenticate(imap, polling.Username, polling.PasswordTrue, account.OAuthProvider);

                IMailFolder inbox = imap.Inbox;
                inbox.Open(FolderAccess.ReadWrite);

                int leftovers = RemoveTestQueries(inbox, null);
                _output.WriteLine("{0} leftover test message(s) removed first", leftovers);

                string queryId = TurnQuery.NewQueryId();
                UniqueId? appended = inbox.Append(TurnQuery.BuildQuery(polling.Username, polling.Username, TestGame, queryId));
                _output.WriteLine("appended query {0} as uid {1}", queryId, appended);

                int removed = RemoveTestQueries(inbox, queryId);
                Assert.True(removed == 1, "the appended query should be found exactly once by its header and removed");

                Assert.Equal(0, RemoveTestQueries(inbox, null));
                _output.WriteLine("removed; nothing of ours remains");

                imap.Disconnect(true);
            }
        }

        /// <summary>
        /// The poller's own path: unseen mail, the header-only fetch, then the delete. Removes every
        /// test query (or just the one with the given id) and returns how many.
        /// </summary>
        private int RemoveTestQueries(IMailFolder inbox, string queryId)
        {
            List<long> unseen = inbox.Search(SearchQuery.NotSeen).Select(uid => (long)uid.Id).ToList();
            HashSet<long> wrapper = ImapPoller.FindWrapperMessages(inbox, unseen);
            _output.WriteLine("{0} unseen, {1} wrapper message(s)", unseen.Count, wrapper.Count);

            List<UniqueId> ours = new List<UniqueId>();
            foreach (long uid in wrapper)
            {
                MimeMessage message = inbox.GetMessage(new UniqueId((uint)uid));
                TurnQueryRequest parsed = TurnQuery.ParseQuery(message);
                if (parsed != null && parsed.Game == TestGame && (queryId == null || parsed.QueryId == queryId))
                {
                    ours.Add(new UniqueId((uint)uid));
                }
            }

            ImapPoller.RemoveWrapperMessages(inbox, ours);
            return ours.Count;
        }
    }
}
