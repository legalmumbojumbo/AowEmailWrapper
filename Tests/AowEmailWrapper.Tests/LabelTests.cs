using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AowEmailWrapper.Controls;
using AowEmailWrapper.Games;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// A label routes turns to exactly one copy, so the label dialog offers every mod label, marks
    /// the ones another copy holds, and choosing one of those moves it.
    /// </summary>
    public class LabelTests : IDisposable
    {
        private readonly string _root;

        public LabelTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "AowEmailWrapper.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        [Fact]
        public void Every_mod_label_is_offered_as_itself()
        {
            AowGame game = Game("copy");

            List<KeyValuePair<string, string>> options = LabelDialog.BuildOptions(game);
            List<string> labels = options.Select(option => option.Value).ToList();

            Assert.Equal("Vanilla 1.36", labels[0]);
            Assert.Contains("Ziggurat", labels);
            Assert.Contains("AoWx", labels);
            Assert.Contains("Evolved", labels);
            Assert.Contains("Dark Lord", labels);
            Assert.Equal(labels.Count, labels.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            //Several copies may share a label, so none is marked as held by another copy
            Assert.All(options, option => Assert.Equal(option.Value, option.Key));
        }

        [Fact]
        public void The_current_label_is_offered_even_when_it_is_not_a_preset()
        {
            AowGame game = Game("named");
            game.Label = "Zig Test";

            List<string> labels = LabelDialog.BuildOptions(game).Select(option => option.Value).ToList();

            Assert.Contains("Zig Test", labels);
        }

        [Fact]
        public void Among_copies_sharing_a_label_the_games_default_then_the_mods_own_executable_decides()
        {
            AowGame old = Game("old");
            AowGame real = Game("real", AowGame.Aow1ZExeName);
            AowGame third = Game("third");
            foreach (AowGame game in new[] { old, real, third })
            {
                game.Label = "Ziggurat";
            }
            List<AowGame> games = new List<AowGame> { old, real, third };

            //Nobody is the game's default: the copy started through AoWz.exe is the mod's home
            Assert.Same(real, AowGameManager.DefaultFor(games, AowGameType.Aow1, "ziggurat"));
            Assert.True(AowGameManager.IsDefaultForItsLabel(games, real));
            Assert.False(AowGameManager.IsDefaultForItsLabel(games, old));

            //The game's default copy, when it carries the label, is the mod's default too
            old.IsDefault = true;
            Assert.Same(old, AowGameManager.DefaultFor(games, AowGameType.Aow1, "Ziggurat"));

            //A label no copy carries has no default; a copy of another game does not count
            Assert.Null(AowGameManager.DefaultFor(games, AowGameType.Aow1, "AoWx"));
            Assert.Null(AowGameManager.DefaultFor(games, AowGameType.AowSm, "Ziggurat"));
        }

        private AowGame Game(string name, string exeName = AowGame.Aow1ExeName)
        {
            string folder = Path.Combine(_root, name);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, exeName), new byte[] { 1 });
            return new AowGame(AowGameType.Aow1, folder, InstallSource.Folder);
        }
    }
}
