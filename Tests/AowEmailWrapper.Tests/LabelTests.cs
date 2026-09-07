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
        public void Every_mod_label_is_offered_and_a_held_one_is_marked()
        {
            AowGame game = Game("copy");
            Dictionary<string, string> taken = new Dictionary<string, string>
            {
                { "Ziggurat", @"E:\Games\Age of Wonders zig" },
                { "AoWx", @"E:\Age of Wonders X" },
            };

            List<KeyValuePair<string, string>> options = LabelDialog.BuildOptions(game, taken);
            List<string> labels = options.Select(option => option.Value).ToList();

            Assert.Equal("Vanilla 1.36", labels[0]);
            Assert.Contains("Ziggurat", labels);
            Assert.Contains("AoWx", labels);
            Assert.Contains("Evolved", labels);
            Assert.Contains("Dark Lord", labels);
            Assert.Equal(labels.Count, labels.Distinct(StringComparer.OrdinalIgnoreCase).Count());

            KeyValuePair<string, string> ziggurat = options.Single(option => option.Value == "Ziggurat");
            Assert.Contains("Age of Wonders zig", ziggurat.Key);
            Assert.Equal("Evolved", options.Single(option => option.Value == "Evolved").Key);
        }

        [Fact]
        public void The_current_label_is_offered_even_when_it_is_not_a_preset()
        {
            AowGame game = Game("named");
            game.Label = "Zig Test";

            List<string> labels = LabelDialog.BuildOptions(game, null).Select(option => option.Value).ToList();

            Assert.Contains("Zig Test", labels);
        }

        [Fact]
        public void Choosing_a_held_label_moves_it_off_the_other_copy()
        {
            AowGame first = Game("first");
            AowGame second = Game("second");
            AowGame otherGame = new AowGame(AowGameType.AowSm, Path.Combine(_root, "sm"), InstallSource.Folder);
            first.Label = "Ziggurat";
            otherGame.Label = "Ziggurat";
            List<AowGame> games = new List<AowGame> { first, second, otherGame };

            GamesConfig.MoveLabel(games, second, "Ziggurat");

            Assert.Equal("Ziggurat", second.Label);
            Assert.Equal(string.Empty, first.Label);
            Assert.Equal("Ziggurat", otherGame.Label); //a different game: labels are per game

            GamesConfig.MoveLabel(games, second, string.Empty);
            Assert.Equal(string.Empty, second.Label);
        }

        private AowGame Game(string name)
        {
            string folder = Path.Combine(_root, name);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, AowGame.Aow1ExeName), new byte[] { 1 });
            return new AowGame(AowGameType.Aow1, folder, InstallSource.Folder);
        }
    }
}
