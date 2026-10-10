using System;
using System.IO;
using System.Xml.Serialization;
using AowEmailWrapper.Classes;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Games;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// A paused game: a turn the player holds but has put aside. No envelope, still playable from EmailIn, and
    /// still the player's turn to anyone who asks.
    /// </summary>
    public class PausedGameTests : IDisposable
    {
        private const string Game = "Nathyazik 2026.asg";
        private readonly string _root;

        public PausedGameTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "AowEmailWrapper.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        private AowGame Install(params string[] subfolders)
        {
            foreach (string sub in subfolders)
            {
                Directory.CreateDirectory(Path.Combine(_root, sub));
            }
            File.Copy(Path.Combine(Environment.SystemDirectory, "whoami.exe"), Path.Combine(_root, AowGame.Aow1ExeName));
            return new AowGame(AowGameType.Aow1, _root, InstallSource.Manual);
        }

        private static Activity Turn(ActivityState status)
        {
            return new Activity(status, AowGameType.Aow1, Game, "Mirrored Oasis of Nathyazik", "3");
        }

        [Fact]
        public void A_paused_turn_shows_no_envelope_but_still_opens_with_its_save()
        {
            ActivityList log = new ActivityList();
            log.Activities.Add(Turn(ActivityState.Paused));
            log.Activities.Add(new Activity(ActivityState.Received, AowGameType.Aow1, "Heulax 26.asg", "Heulax", "2"));
            Assert.Equal(1, log.GetUnSentActivitiesCount());

            AowGame game = Install("EmailIn");
            File.WriteAllBytes(Path.Combine(game.EmailIn.FullName, Game), new byte[] { 1, 2, 3 });
            Assert.Equal(Path.Combine("EmailIn", Game), Main.SaveToLoad(game, Turn(ActivityState.Paused)));
        }

        [Fact]
        public void Other_wrappers_are_told_a_paused_turn_is_still_held()
        {
            Activity paused = Turn(ActivityState.Paused);

            TurnState state = TurnQuery.StateOf(paused, Game, "abc123");

            Assert.Equal(ActivityState.Received, state.Status);
            Assert.True(state.Holds);
        }

        [Fact]
        public void A_paused_game_survives_the_activity_log()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Activity));
            StringWriter writer = new StringWriter();
            serializer.Serialize(writer, Turn(ActivityState.Paused));

            Assert.Contains("status=\"Paused\"", writer.ToString());
            Assert.Equal(ActivityState.Paused, ((Activity)serializer.Deserialize(new StringReader(writer.ToString()))).Status);
        }

        [Fact]
        public void Pausing_a_game_ended_by_mistake_brings_its_files_back_from_the_ended_folders()
        {
            AowGame game = Install("EmailIn", "EmailOut", Path.Combine("EmailIn", "Ended"), Path.Combine("EmailOut", "Ended"));
            File.WriteAllText(Path.Combine(game.EmailIn.FullName, "Ended", Game), "received");
            File.WriteAllText(Path.Combine(game.EmailOut.FullName, "Ended", Game), "archived sent");
            File.WriteAllText(Path.Combine(game.EmailOut.FullName, Game), "newer sent");
            AowGameManager manager = new AowGameManager(Path.Combine(_root, "CheckEmail"), new[] { game }, new GamesConfigValues());

            manager.RestoreEndedGame(AowGameType.Aow1, Game, "Ended");

            Assert.Equal("received", File.ReadAllText(Path.Combine(game.EmailIn.FullName, Game)));
            Assert.False(File.Exists(Path.Combine(game.EmailIn.FullName, "Ended", Game)));
            //A file already back in its folder is kept, and the archived one left alone
            Assert.Equal("newer sent", File.ReadAllText(Path.Combine(game.EmailOut.FullName, Game)));
            Assert.True(File.Exists(Path.Combine(game.EmailOut.FullName, "Ended", Game)));
        }
    }
}
