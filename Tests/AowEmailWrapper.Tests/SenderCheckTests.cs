using System;
using System.IO;
using System.Xml.Serialization;
using AowEmailWrapper.ASG;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Games;
using AowEmailWrapper.Helpers;
using MimeKit;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// The Wrapper files a turn from anyone who emails one, so a turn from an address that has never
    /// sent or received a turn before is marked for the player.
    /// </summary>
    public class SenderCheckTests
    {
        [Fact]
        public void An_address_that_sent_a_turn_before_is_known()
        {
            ActivityList log = new ActivityList();
            log.Activities.Add(new Activity(ActivityState.Received, AowGameType.Aow1, "Game.asg", "Map", "3") { Sender = "Bob@Example.com" });

            Assert.True(log.IsKnownAddress("bob@example.com"));
            Assert.True(log.IsKnownAddress("  BOB@EXAMPLE.COM "));
            Assert.False(log.IsKnownAddress("eve@example.com"));
        }

        [Fact]
        public void An_address_the_player_sent_a_turn_to_is_known()
        {
            ActivityList log = new ActivityList();
            log.Activities.Add(new Activity(ActivityState.Sent, AowGameType.Aow1, "Game.asg", "Map", "3") { Recipients = "alice@example.com;carol@example.org" });

            Assert.True(log.IsKnownAddress("carol@example.org"));
            Assert.True(log.IsKnownAddress("alice@example.com"));
            Assert.False(log.IsKnownAddress("eve@example.com"));
        }

        [Fact]
        public void Blank_addresses_are_never_known()
        {
            ActivityList log = new ActivityList();
            log.Activities.Add(new Activity(ActivityState.Received, AowGameType.Aow1, "Game.asg", "Map", "3") { Sender = "" });

            Assert.False(log.IsKnownAddress(null));
            Assert.False(log.IsKnownAddress(""));
            Assert.False(log.IsKnownAddress("   "));
        }

        [Fact]
        public void Sender_and_new_sender_mark_survive_the_activity_file()
        {
            Activity activity = new Activity(ActivityState.Received, AowGameType.Aow1, "Game.asg", "Map", "3")
            {
                Sender = "eve@example.com",
                NewSender = true
            };

            string xml = Serialize(activity);
            Assert.Contains("sender=\"eve@example.com\"", xml);
            Assert.Contains("new_sender=\"true\"", xml);

            Activity back = Deserialize(xml);
            Assert.Equal("eve@example.com", back.Sender);
            Assert.True(back.NewSender);
        }

        [Fact]
        public void Known_sender_mark_is_omitted_and_old_records_read_as_known()
        {
            string xml = Serialize(new Activity(ActivityState.Received, AowGameType.Aow1, "Game.asg", "Map", "3") { Sender = "bob@example.com" });
            Assert.DoesNotContain("new_sender", xml);

            const string oldXml = "<activity game_type=\"Aow1\" file_name=\"Highpass.asg\" map_title=\"Highpass\" turn=\"3\" status=\"Received\" ticks=\"1\" />";
            Activity old = Deserialize(oldXml);
            Assert.False(old.NewSender);
            Assert.Null(old.Sender);
        }

        [Fact]
        public void Recipients_of_a_sent_turn_are_recorded_once_each()
        {
            MimeMessage message = new MimeMessage();
            message.To.Add(new MailboxAddress("Alice", "alice@example.com"));
            message.To.Add(new MailboxAddress("Alice again", "ALICE@example.com"));
            message.Cc.Add(new MailboxAddress("Carol", "carol@example.org"));
            message.Bcc.Add(new MailboxAddress("Dave", "dave@example.net"));

            Assert.Equal("alice@example.com;carol@example.org;dave@example.net", MailHelper.GetRecipientAddresses(message));
            Assert.Equal(string.Empty, MailHelper.GetRecipientAddresses(null));
        }

        [Fact]
        public void Stored_turn_carries_its_sender_to_the_saved_event()
        {
            string holding = Path.Combine(Path.GetTempPath(), "AowEmailWrapper.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(holding);
            try
            {
                AowGameManager manager = new AowGameManager(holding, new AowGame[0], null);
                AowGameSavedEventArgs saved = null;
                manager.OnGameSaved += (sender, e) => saved = e;

                using (ASGFileInfo info = new ASGFileInfo("Stranger.asg", new byte[] { 1, 2, 3 }))
                {
                    manager.StoreDownloadFile(info, EmailSaveFolder.EmailIn, "Account", null, "eve@example.com");
                }

                Assert.NotNull(saved);
                Assert.Equal("eve@example.com", saved.Sender);
                Assert.Equal("eve@example.com", new Activity(saved).Sender);
            }
            finally
            {
                try { Directory.Delete(holding, true); } catch { }
            }
        }

        private static string Serialize(Activity activity)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Activity));
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, activity);
                return writer.ToString();
            }
        }

        private static Activity Deserialize(string xml)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Activity));
            using (StringReader reader = new StringReader(xml))
            {
                return (Activity)serializer.Deserialize(reader);
            }
        }
    }
}
