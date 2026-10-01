using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Controls;
using AowEmailWrapper.Helpers;
using MimeKit;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// The Aliases tab's list: one name per address, kept between runs, and shown in place of the
    /// address wherever the Wrapper shows one. The names in use are process-wide, so these tests run
    /// on their own.
    /// </summary>
    [Collection(nameof(ProcessStateCollection))]
    public class AliasTests : IDisposable
    {
        private const string Bob = "bob@example.com";
        private const string BobAtWork = "bob@work.example.org";
        private const string Carol = "carol@example.org";

        private readonly AliasList _before = AliasHelper.Current;

        public void Dispose()
        {
            AliasHelper.Current = _before;
        }

        private static AliasList BobAndCarol()
        {
            AliasList list = new AliasList();
            list.Set("Bob the Builder", Bob, null);
            list.Set("Bob the Builder", BobAtWork, null);
            list.Set("Carol", Carol, null);
            return list;
        }

        [Fact]
        public void An_address_has_one_name_and_a_name_may_have_several_addresses()
        {
            AliasList list = BobAndCarol();

            Assert.Equal("Bob the Builder", list.NameFor(" BOB@example.com "));
            Assert.Equal("Bob the Builder", list.NameFor(BobAtWork));
            Assert.Null(list.NameFor("dave@example.net"));
            Assert.Equal(new[] { "Bob the Builder", "Carol" }, list.Names);

            //Naming an address again renames it rather than adding a second entry
            list.Set("Robert", Bob, null);
            Assert.Equal(3, list.Aliases.Count);
            Assert.Equal("Robert", list.NameFor(Bob));

            //An edit may change the address: the old one goes
            list.Set("Carol", "carol@new.example.org", Carol);
            Assert.Null(list.NameFor(Carol));
            Assert.Equal("Carol", list.NameFor("carol@new.example.org"));

            Assert.True(list.Remove(BobAtWork));
            Assert.False(list.Remove(BobAtWork));
            Assert.Equal(2, list.Aliases.Count);
        }

        [Fact]
        public void The_list_survives_its_file_and_a_copy_is_independent()
        {
            AliasList list = BobAndCarol();
            XmlSerializer serializer = new XmlSerializer(typeof(AliasList));
            string xml;
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, list);
                xml = writer.ToString();
            }
            AliasList back;
            using (StringReader reader = new StringReader(xml))
            {
                back = (AliasList)serializer.Deserialize(reader);
            }

            Assert.Equal(3, back.Aliases.Count);
            Assert.Equal("Carol", back.NameFor(Carol));

            AliasList copy = back.Clone();
            copy.Remove(Carol);
            Assert.Equal("Carol", back.NameFor(Carol));
        }

        [Fact]
        public void Addresses_show_as_names_wherever_the_player_named_them()
        {
            AliasHelper.Current = BobAndCarol();

            Assert.Equal("Carol", AliasHelper.Display(Carol));
            Assert.Equal("dave@example.net", AliasHelper.Display("dave@example.net"));
            Assert.Null(AliasHelper.Display(null));
            Assert.Equal("Carol (carol@example.org), dave@example.net", AliasHelper.DisplayListWithAddresses(Carol + ";dave@example.net;"));
        }

        [Fact]
        public void Only_whole_addresses_in_a_message_are_replaced()
        {
            AliasList list = BobAndCarol();
            list.Set("$1 Dollar", "dollar@example.com", null);
            AliasHelper.Current = list;

            Assert.Equal("Sent to [Bob the Builder] successfully.", AliasHelper.InText("Sent to [BOB@example.com] successfully."));
            Assert.Equal("Carol has held 'x.asg' since 1 Oct. Carol sent it.", AliasHelper.InText("carol@example.org has held 'x.asg' since 1 Oct. carol@example.org sent it."));
            //Another address that merely contains a named one stays as it is
            Assert.Equal("xbob@example.com and bob@example.com.au", AliasHelper.InText("xbob@example.com and bob@example.com.au"));
            //A name is shown as typed, even one that looks like a replacement pattern
            Assert.Equal("from $1 Dollar", AliasHelper.InText("from dollar@example.com"));
            Assert.Equal(string.Empty, AliasHelper.InText(string.Empty));
        }

        [Fact]
        public void The_dialog_takes_a_name_and_one_address_that_is_not_someone_elses()
        {
            AliasList list = BobAndCarol();

            Assert.NotNull(AliasDialog.Problem(" ", "dave@example.net", list, null));
            foreach (string bad in new[] { "dave", "dave@", "@example.net", "dave@example.net;eve@example.net", "dave smith@example.net", "a@b@c" })
            {
                Assert.NotNull(AliasDialog.Problem("Dave", bad, list, null));
            }
            Assert.Null(AliasDialog.Problem("Dave", " dave@example.net ", list, null));

            //Carol's address belongs to Carol, unless it is Carol's entry being edited
            Assert.Contains("Carol", AliasDialog.Problem("Dave", Carol, list, null));
            Assert.Null(AliasDialog.Problem("Caroline", Carol.ToUpperInvariant(), list, Carol));
        }
    
        private const string Dave = "dave@example.net";
        private const string Me = "me@example.com";
        private const string Stranger = "stranger@evil.example";

        [Fact]
        public void A_turn_shares_the_players_own_name_and_the_names_of_the_other_players_in_the_game()
        {
            AliasList list = BobAndCarol();
            list.Learn("Dave the Dwarf", Dave, Carol);

            List<PlayerAlias> shared = AliasHelper.NamesToShare(list, " Eugene ", Me, new[] { Bob, Dave, "nobody@example.net", Me, Bob });

            Assert.Equal(new[] { "Eugene <me@example.com>", "Bob the Builder <bob@example.com>", "Dave the Dwarf <dave@example.net>" },
                shared.Select(alias => alias.Name + " <" + alias.Address + ">"));
            //Carol is named on this PC but is not in this game, so her name does not go out with it
            Assert.DoesNotContain(shared, alias => alias.Address == Carol);
            Assert.Empty(AliasHelper.NamesToShare(new AliasList(), null, Me, new[] { Bob }));
        }

        [Fact]
        public void Only_a_known_sender_names_players_and_only_players_of_that_game_never_the_player()
        {
            AliasList list = new AliasList();
            list.Set("Bob the Builder", Bob, null);
            PlayerAlias[] shared =
            {
                new PlayerAlias("Bobby", Bob),            //already named here: kept as it is
                new PlayerAlias("Carol", Carol),          //a player of the game: learned
                new PlayerAlias("The Boss", Me),          //the player's own address: never
                new PlayerAlias("Dave", Dave),            //not a player of this game: ignored
            };

            Assert.Equal(0, AliasHelper.LearnFromTurn(list, shared, Stranger, false, new[] { Bob, Carol, Me }, new[] { Me }));
            Assert.Null(list.Find(Carol));

            Assert.Equal(1, AliasHelper.LearnFromTurn(list, shared, Bob, true, new[] { Bob, Carol, Me }, new[] { Me }));
            Assert.Equal("Bob the Builder", list.NameFor(Bob));
            Assert.Equal("Carol", list.NameFor(Carol));
            Assert.Equal(Bob, list.Find(Carol).SharedBy);
            Assert.Null(list.Find(Me));
            Assert.Null(list.Find(Dave));
        }

        [Fact]
        public void A_learned_name_shows_but_does_not_make_its_address_a_known_sender_until_the_player_names_it()
        {
            AliasList list = new AliasList();
            Assert.True(list.Learn("Carol", Carol, Bob));
            Assert.False(list.Learn("Caroline", Carol, Dave));
            Assert.Equal("Carol", list.NameFor(Carol));
            Assert.Null(list.FindOwn(Carol));

            //Editing a learned name on the Aliases tab makes it the player's own
            list.Set("Carol", Carol, Carol);
            Assert.NotNull(list.FindOwn(Carol));
            Assert.False(list.Find(Carol).IsShared);
            Assert.False(list.Clone().Find(Carol).IsShared);

            list.Learn("Dave", Dave, Bob);
            Assert.Equal(Bob, list.Clone().Find(Dave).SharedBy);
        }

        [Fact]
        public void A_learned_name_survives_a_restart_with_who_shared_it()
        {
            AliasList list = new AliasList();
            list.Set("Bob the Builder", Bob, null);
            list.Learn("Carol", Carol, Bob);

            XmlSerializer serializer = new XmlSerializer(typeof(AliasList));
            StringWriter writer = new StringWriter();
            serializer.Serialize(writer, list);
            AliasList read = (AliasList)serializer.Deserialize(new StringReader(writer.ToString()));

            Assert.False(read.Find(Bob).IsShared);
            Assert.Equal(Bob, read.Find(Carol).SharedBy);
        }

        [Theory]
        [InlineData("Bob the Builder")]
        [InlineData("Smith, Bob")]
        [InlineData("Bob \"the Hammer\" Smith")]
        [InlineData("Björn Ångström")]
        [InlineData("Мирослав")]
        public void Names_come_through_the_turn_email_whole(string name)
        {
            MimeMessage sent = new MimeMessage();
            sent.From.Add(new MailboxAddress(string.Empty, Me));
            sent.Body = new TextPart("plain") { Text = "turn" };
            MailHelper.SetSharedNames(sent, new[] { new PlayerAlias(name, Bob), new PlayerAlias("Carol", Carol) });

            MemoryStream wire = new MemoryStream();
            sent.WriteTo(wire);
            string raw = System.Text.Encoding.UTF8.GetString(wire.ToArray());
            string header = raw.Split(new[] { "\r\n" }, StringSplitOptions.None).First(line => line.StartsWith(MailHelper.NamesHeaderName));
            Assert.True(header.All(c => c < 128), "the header goes out as plain ASCII: " + header);

            wire.Position = 0;
            List<PlayerAlias> read = MailHelper.GetSharedNames(MimeMessage.Load(wire));
            Assert.Equal(new[] { name + "|" + Bob, "Carol|" + Carol }, read.Select(alias => alias.Name + "|" + alias.Address));
        }

        [Fact]
        public void A_turn_without_names_or_with_a_broken_header_names_nobody()
        {
            MimeMessage message = new MimeMessage();
            Assert.Empty(MailHelper.GetSharedNames(message));
            message.Headers.Add(MailHelper.NamesHeaderName, "<<<not an address list");
            Assert.Empty(MailHelper.GetSharedNames(message));
            MailHelper.SetSharedNames(message, new PlayerAlias[0]);
            Assert.Null(message.Headers[MailHelper.NamesHeaderName]);
        }
}
}
