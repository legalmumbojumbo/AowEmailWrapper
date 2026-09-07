using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Games;
using AowEmailWrapper.Helpers;
using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using Xunit;
using Xunit.Abstractions;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// Opponents from before the contact list existed are found in the resend folder and the mailbox,
    /// and turns the player still owes are worked out from the same scan so a fresh install picks
    /// up where the old one left off.
    /// </summary>
    public class ContactHistoryTests
    {
        private readonly ITestOutputHelper _output;
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

        public ContactHistoryTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Contacts

        [Fact]
        public void Contacts_make_an_address_known_and_survive_the_activity_file()
        {
            ActivityList log = new ActivityList();
            Assert.True(log.AddContact("Bob@Example.com"));
            Assert.False(log.AddContact(" bob@example.com "));
            Assert.False(log.AddContact(""));
            Assert.Equal(2, log.AddContacts(new[] { "alice@example.com;bob@example.com", "carol@example.org" }));

            Assert.True(log.IsKnownAddress("bob@example.com"));
            Assert.True(log.IsKnownAddress("CAROL@example.org"));
            Assert.False(log.IsKnownAddress("eve@example.com"));

            log.HistoryImported = true;
            ActivityList back = RoundTrip(log);
            Assert.Equal(new[] { "Bob@Example.com", "alice@example.com", "carol@example.org" }, back.Contacts);
            Assert.True(back.HistoryImported);
            Assert.True(back.IsKnownAddress("alice@example.com"));
        }

        [Fact]
        public void An_old_activity_file_has_no_contacts_and_no_import_yet()
        {
            const string oldXml = "<activities><activity game_type=\"Aow1\" file_name=\"Highpass.asg\" map_title=\"Highpass\" turn=\"3\" status=\"Received\" ticks=\"1\" /></activities>";
            ActivityList old;
            using (StringReader reader = new StringReader(oldXml))
            {
                old = (ActivityList)new XmlSerializer(typeof(ActivityList)).Deserialize(reader);
            }

            Assert.Empty(old.Contacts);
            Assert.False(old.HistoryImported);
            Assert.Single(old.Activities);

            string xml = Serialize(new ActivityList());
            Assert.DoesNotContain("history_imported", xml);
        }

        [Fact]
        public void Resend_files_name_the_people_the_player_sends_turns_to()
        {
            DirectoryInfo resend = AppDataHelper.Resend;
            resend.Create();
            string path = Path.Combine(resend.FullName, "History Test.asg_resend.eml");
            try
            {
                MimeMessage message = new MimeMessage();
                message.From.Add(new MailboxAddress("Me", "me@example.com"));
                message.To.Add(new MailboxAddress("Alice", "alice@example.com"));
                message.Cc.Add(new MailboxAddress("Carol", "carol@example.org"));
                message.Subject = "AoW";
                message.Body = new TextPart("plain") { Text = "Your turn" };
                message.WriteTo(path);

                HashSet<string> addresses = ContactHistory.FromResendFiles();

                Assert.Contains("alice@example.com", addresses);
                Assert.Contains("carol@example.org", addresses);
                Assert.DoesNotContain("me@example.com", addresses);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Only_messages_with_a_save_game_count()
        {
            BodyPartBasic save = new BodyPartBasic { ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = "Game.asg" } };
            BodyPartBasic picture = new BodyPartBasic { ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = "photo.jpg" } };
            BodyPartBasic named = new BodyPartBasic { ContentType = new ContentType("application", "octet-stream") { Name = "Turn 4.ASG" } };

            Assert.True(ContactHistory.HasSaveGame(new[] { picture, save }));
            Assert.Equal("Game.asg", ContactHistory.SaveGameName(new[] { picture, save }));
            Assert.Equal("Turn 4.ASG", ContactHistory.SaveGameName(new[] { named }));
            Assert.False(ContactHistory.HasSaveGame(new[] { picture }));
            Assert.False(ContactHistory.HasSaveGame(new BodyPartBasic[0]));
            Assert.False(ContactHistory.HasSaveGame(null));
        }

        [Fact]
        public void Received_messages_give_the_sender_and_sent_messages_the_recipients()
        {
            Envelope envelope = new Envelope();
            envelope.From.Add(new MailboxAddress("Bob", "bob@example.com"));
            envelope.To.Add(new MailboxAddress("Alice", "alice@example.com"));
            envelope.To.Add(new MailboxAddress("Alice again", "ALICE@example.com"));
            envelope.Cc.Add(new MailboxAddress("Carol", "carol@example.org"));

            Assert.Equal(new[] { "bob@example.com" }, ContactHistory.AddressesOf(envelope, false).ToArray());
            Assert.Equal(new[] { "alice@example.com", "carol@example.org" }, ContactHistory.AddressesOf(envelope, true).ToArray());
            Assert.Empty(ContactHistory.AddressesOf(null, true));
        }

        #endregion

        #region Unplayed turns

        [Theory]
        [InlineData("Midwinter (Dave, Fred) Upatch 1.4.asg", "midwinter (dave, fred) upatch 1")]
        [InlineData("Midwinter (Dave, Fred) Upatch 1.asg", "midwinter (dave, fred) upatch 1")]
        [InlineData("Highpass .asg", "highpass")]
        [InlineData("  HIGHPASS.ASG ", "highpass")]
        [InlineData("nodot", "nodot")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Game_key_ignores_the_games_truncation_at_a_dot_and_case(string fileName, string expected)
        {
            Assert.Equal(expected, ContactHistory.GameKey(fileName));
        }

        [Fact]
        public void A_received_turn_newer_than_anything_sent_for_that_game_is_unplayed()
        {
            List<MailboxTurn> received = new List<MailboxTurn>
            {
                Turn(1, "Highpass.asg", -10, "bob@example.com"),
                Turn(2, "Highpass.asg", -2, "bob@example.com"),
                Turn(3, "Thin Lake.asg", -5, "carol@example.org"),
                Turn(4, "Old Game.asg", -400, "bob@example.com"),
            };
            List<MailboxTurn> sent = new List<MailboxTurn>
            {
                Turn(10, "Highpass.asg", -6, "me@example.com"),
                Turn(11, "Thin Lake.asg", -4, "me@example.com"),
            };

            List<MailboxTurn> unplayed = ContactHistory.FindUnplayed(received, sent, new[] { "me@example.com" }, Now, 365);

            //Highpass: turn 2 arrived after the last turn sent. Thin Lake was answered. Old Game is outside the window.
            MailboxTurn only = Assert.Single(unplayed);
            Assert.Equal(2u, only.Uid);
        }

        [Fact]
        public void A_game_never_answered_is_unplayed_and_the_newest_turn_is_chosen()
        {
            List<MailboxTurn> received = new List<MailboxTurn>
            {
                Turn(1, "Highpass.asg", -20, "bob@example.com"),
                Turn(2, "Highpass.asg", -3, "bob@example.com"),
                Turn(3, "Highpass.asg", -8, "bob@example.com"),
            };

            List<MailboxTurn> unplayed = ContactHistory.FindUnplayed(received, new List<MailboxTurn>(), new[] { "me@example.com" }, Now, 365);

            MailboxTurn only = Assert.Single(unplayed);
            Assert.Equal(2u, only.Uid);
        }

        [Fact]
        public void A_copy_from_the_players_own_address_counts_as_sent()
        {
            //The BCC-myself option puts the player's outgoing turn in the inbox
            List<MailboxTurn> received = new List<MailboxTurn>
            {
                Turn(1, "Highpass.asg", -5, "bob@example.com"),
                Turn(2, "Highpass.asg", -4, "ME@example.com"),
            };

            Assert.Empty(ContactHistory.FindUnplayed(received, null, new[] { "me@example.com" }, Now, 365));
            Assert.Single(ContactHistory.FindUnplayed(received, null, null, Now, 365));
        }

        [Fact]
        public void The_games_truncated_file_name_still_matches_the_sent_turn()
        {
            List<MailboxTurn> received = new List<MailboxTurn> { Turn(1, "Midwinter Upatch 1.4.asg", -5, "bob@example.com") };
            List<MailboxTurn> sent = new List<MailboxTurn> { Turn(2, "Midwinter Upatch 1.asg", -1, "me@example.com") };

            Assert.Empty(ContactHistory.FindUnplayed(received, sent, new[] { "me@example.com" }, Now, 365));
        }

        [Fact]
        public void Unplayed_turns_come_newest_first()
        {
            List<MailboxTurn> received = new List<MailboxTurn>
            {
                Turn(1, "A.asg", -9, "bob@example.com"),
                Turn(2, "B.asg", -1, "bob@example.com"),
                Turn(3, "C.asg", -5, "bob@example.com"),
            };

            Assert.Equal(new uint[] { 2, 3, 1 }, ContactHistory.FindUnplayed(received, null, null, Now, 365).Select(t => t.Uid).ToArray());
        }

        #endregion

        [LiveFact]
        public void Every_imap_account_yields_its_history()
        {
            string path = Path.Combine(TestEnvironment.RealAppData, "AowEmailWrapper", "Config", "config.xml");
            Config config = FileHelper.LoadXmlFile<Config>(path);
            Assert.True(config != null && config.AccountsList != null && config.AccountsList.Accounts.Count > 0, "no accounts configured in " + path);

            foreach (AccountConfigValues account in config.AccountsList.Accounts)
            {
                PollingConfigValues polling = account.PollingConfig;
                if (polling == null || polling.EmailType != EmailType.IMAP || MicrosoftOAuth.IsProvider(account.OAuthProvider))
                {
                    continue;
                }

                List<string> own = new List<string> { polling.Username };
                if (account.SmtpConfig != null && !string.IsNullOrEmpty(account.SmtpConfig.EmailAddress))
                {
                    own.Add(account.SmtpConfig.EmailAddress);
                }

                using (ImapClient imap = new ImapClient())
                {
                    imap.Connect(polling.Server, polling.Port, MailHelper.ToSecureSocketOptions(polling.SSLType));
                    MailHelper.Authenticate(imap, polling.Username, polling.PasswordTrue, account.OAuthProvider);

                    MailboxHistory history = ContactHistory.Scan(imap, own, 365, System.Threading.CancellationToken.None);

                    _output.WriteLine("{0}: {1} past opponents, {2} unplayed turn(s)", account.Name, history.Addresses.Count, history.Unplayed.Count);
                    foreach (MailboxTurn turn in history.Unplayed)
                    {
                        _output.WriteLine("  unplayed: {0}", turn);
                    }
                    Assert.True(imap.Inbox.IsOpen, "the inbox should be left open for the poller");
                    imap.Disconnect(true);
                }
            }
        }

        private static MailboxTurn Turn(uint uid, string fileName, int daysAgo, string from)
        {
            return new MailboxTurn { Uid = uid, FileName = fileName, Date = Now.AddDays(daysAgo), From = from };
        }

        private static string Serialize(ActivityList log)
        {
            using (StringWriter writer = new StringWriter())
            {
                new XmlSerializer(typeof(ActivityList)).Serialize(writer, log);
                return writer.ToString();
            }
        }

        private static ActivityList RoundTrip(ActivityList log)
        {
            using (StringReader reader = new StringReader(Serialize(log)))
            {
                return (ActivityList)new XmlSerializer(typeof(ActivityList)).Deserialize(reader);
            }
        }
    }
}
