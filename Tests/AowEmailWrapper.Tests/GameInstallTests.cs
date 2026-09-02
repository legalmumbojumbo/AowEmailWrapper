using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Games;
using AowEmailWrapper.Helpers;
using MimeKit;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// Several copies of one game for different mods: detection by folder, the labels and
    /// defaults from config, and the routing of turns in and out.
    /// </summary>
    public class GameInstallTests : IDisposable
    {
        private const string GameFile = "Test Game (Dave, Fred).asg";

        private readonly string _root;
        private readonly string _zig;
        private readonly string _vanilla;
        private readonly string _aow1;
        private readonly string _checkEmail;

        public GameInstallTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "AowEmailWrapper.Tests", "games-" + Guid.NewGuid().ToString("N"));
            _zig = MakeGame("Age of Wonders Ziggurat", AowGame.AowSmExeName, AowGame.AowMpeExeName);
            _vanilla = MakeGame("Age of Wonders Vanilla", AowGame.AowSmExeName);
            _aow1 = MakeGame("Age of Wonders", AowGame.Aow1ExeName);
            _checkEmail = Path.Combine(_root, "CheckEmail");
            Directory.CreateDirectory(_checkEmail);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        private string MakeGame(string name, params string[] exes)
        {
            string folder = Path.Combine(_root, name);
            Directory.CreateDirectory(Path.Combine(folder, "EmailIn"));
            Directory.CreateDirectory(Path.Combine(folder, "EmailOut"));
            Directory.CreateDirectory(Path.Combine(folder, "Save"));
            foreach (string exe in exes)
            {
                File.WriteAllBytes(Path.Combine(folder, exe), new byte[] { 0x4D, 0x5A });
            }
            return folder;
        }

        private AowGameManager Manager(GamesConfigValues config = null)
        {
            List<AowGame> installs = new List<AowGame>();
            installs.AddRange(GameDetector.ScanFolder(_zig, InstallSource.Folder));
            installs.AddRange(GameDetector.ScanFolder(_vanilla, InstallSource.Registry));
            installs.AddRange(GameDetector.ScanFolder(_aow1, InstallSource.Steam));
            return new AowGameManager(_checkEmail, installs, config);
        }

        private static GamesConfigValues Labels(string zigFolder, string vanillaFolder)
        {
            GamesConfigValues config = new GamesConfigValues();
            config.Installs.Add(new GameInstallConfigValues { GameType = AowGameType.AowSm, Folder = zigFolder, Label = "Ziggurat" });
            config.Installs.Add(new GameInstallConfigValues { GameType = AowGameType.AowSm, Folder = vanillaFolder, Label = "Vanilla", IsDefault = true });
            return config;
        }

        [Fact]
        public void ScanFolder_FindsEveryGameWhoseExeIsPresent()
        {
            List<AowGame> zig = GameDetector.ScanFolder(_zig, InstallSource.Folder);
            Assert.Equal(new[] { AowGameType.AowSm, AowGameType.AowMpe }, zig.Select(game => game.GameType).ToArray());
            Assert.All(zig, game => Assert.True(game.IsInstalled));
            Assert.Equal(Path.Combine(_zig, "EmailIn"), zig[0].EmailIn.FullName);
            Assert.Equal(Path.Combine(_zig, "Save"), zig[0].Save.FullName);

            Assert.Empty(GameDetector.ScanFolder(_checkEmail, InstallSource.Folder));
            Assert.Empty(GameDetector.ScanFolder(Path.Combine(_root, "nowhere"), InstallSource.Manual));
        }

        [Fact]
        public void Merge_AppliesLabelsPicksOneDefaultPerTypeAndKeepsMissingManualFolders()
        {
            GamesConfigValues config = Labels(_zig, _vanilla);
            string gone = Path.Combine(_root, "Deleted copy");
            config.Installs.Add(new GameInstallConfigValues { GameType = AowGameType.Aow2, Folder = gone, Label = "Old", Manual = true });

            AowGameManager manager = Manager(config);

            List<AowGame> sm = manager.GetInstalls(AowGameType.AowSm);
            Assert.Equal(2, sm.Count);
            Assert.Equal("Vanilla", sm[0].Label);
            Assert.True(sm[0].IsDefault);
            Assert.Equal("Ziggurat", sm[1].Label);
            Assert.False(sm[1].IsDefault);

            //A type without a configured default gets one, from the most trustworthy source
            AowGame aow1 = manager.GetGameByType(AowGameType.Aow1);
            Assert.NotNull(aow1);
            Assert.True(aow1.IsDefault);
            Assert.True(manager.GetGameByType(AowGameType.AowMpe).IsDefault);

            //The missing manual folder is listed but not installed, and not offered for routing
            AowGame missing = manager.Games.Single(game => game.GameType == AowGameType.Aow2);
            Assert.False(missing.IsInstalled);
            Assert.Equal("Old", missing.Label);
            Assert.Null(manager.GetGameByType(AowGameType.Aow2));

            GamesConfigValues saved = manager.ToConfig();
            Assert.Contains(saved.Installs, install => install.Label == "Ziggurat" && install.GameType == AowGameType.AowSm && !install.IsDefault);
            Assert.Contains(saved.Installs, install => install.Label == "Old" && install.Manual);
        }

        [Fact]
        public void ResolveIncoming_PrefersLabelThenHintThenHoldingCopyThenDefault()
        {
            AowGameManager manager = Manager(Labels(_zig, _vanilla));
            AowGame zig = manager.GetGameByLabel(AowGameType.AowSm, "Ziggurat");
            AowGame vanilla = manager.GetGameByLabel(AowGameType.AowSm, "Vanilla");

            //Label wins, whatever the spelling
            Assert.Same(zig, manager.ResolveIncoming(AowGameType.AowSm, "zig gurat", GameFile));
            Assert.Same(zig, manager.ResolveIncoming(AowGameType.AowSm, "ZIGGURAT", GameFile));

            //Unknown label and no history: the default copy
            Assert.Same(vanilla, manager.ResolveIncoming(AowGameType.AowSm, "Unheard of", GameFile));
            Assert.Same(vanilla, manager.ResolveIncoming(AowGameType.AowSm, null, GameFile));

            //A copy that already holds a turn of the game gets the next one
            File.WriteAllBytes(Path.Combine(zig.EmailOut.FullName, GameFile), new byte[] { 1 });
            Assert.Same(zig, manager.ResolveIncoming(AowGameType.AowSm, null, GameFile));

            //The activity log's memory of the copy beats the folder search
            manager.InstallHint = (type, file) => _vanilla;
            Assert.Same(vanilla, manager.ResolveIncoming(AowGameType.AowSm, null, GameFile));

            //A single copy needs no deciding, a missing game gives nothing
            Assert.Same(manager.GetGameByType(AowGameType.Aow1), manager.ResolveIncoming(AowGameType.Aow1, "Ziggurat", GameFile));
            Assert.Null(manager.ResolveIncoming(AowGameType.Aow2, null, GameFile));
        }

        [Fact]
        public void ResolveOutgoing_UsesHistoryThenTheCopyHoldingTheFileThenDefault()
        {
            AowGameManager manager = Manager(Labels(_zig, _vanilla));
            AowGame zig = manager.GetGameByLabel(AowGameType.AowSm, "Ziggurat");
            AowGame vanilla = manager.GetGameByLabel(AowGameType.AowSm, "Vanilla");

            Assert.Same(vanilla, manager.ResolveOutgoing(AowGameType.AowSm, GameFile));

            File.WriteAllBytes(Path.Combine(zig.Save.FullName, GameFile), new byte[] { 1 });
            Assert.Same(zig, manager.ResolveOutgoing(AowGameType.AowSm, GameFile));

            //Both copies hold it: ambiguous, so the default
            File.WriteAllBytes(Path.Combine(vanilla.Save.FullName, GameFile), new byte[] { 1 });
            Assert.Same(vanilla, manager.ResolveOutgoing(AowGameType.AowSm, GameFile));

            manager.InstallHint = (type, file) => _zig;
            Assert.Same(zig, manager.ResolveOutgoing(AowGameType.AowSm, GameFile));
        }

        [Fact]
        public void MoveGame_MovesTheTurnFilesIntoTheTargetCopy()
        {
            AowGameManager manager = Manager(Labels(_zig, _vanilla));
            AowGame zig = manager.GetGameByLabel(AowGameType.AowSm, "Ziggurat");
            AowGame vanilla = manager.GetGameByLabel(AowGameType.AowSm, "Vanilla");

            File.WriteAllBytes(Path.Combine(vanilla.EmailIn.FullName, GameFile), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(vanilla.Save.FullName, "Test Game (Dave, Fred)_2.asg"), new byte[] { 2 });
            File.WriteAllBytes(Path.Combine(vanilla.Save.FullName, "Other Game.asg"), new byte[] { 3 });

            manager.MoveGame(AowGameType.AowSm, GameFile, zig);

            Assert.True(File.Exists(Path.Combine(zig.EmailIn.FullName, GameFile)));
            Assert.True(File.Exists(Path.Combine(zig.Save.FullName, "Test Game (Dave, Fred)_2.asg")));
            Assert.False(File.Exists(Path.Combine(vanilla.EmailIn.FullName, GameFile)));
            Assert.True(File.Exists(Path.Combine(vanilla.Save.FullName, "Other Game.asg")));
            Assert.True(zig.HoldsGameFile(GameFile));
            Assert.False(vanilla.HoldsGameFile(GameFile));
        }

        [Fact]
        public void ScanTree_FindsCopiesAtTheRootBesideEachOtherAndNestedInsideOtherCopies()
        {
            string tree = Path.Combine(_root, "Drive");
            string steamCopy = Path.Combine(tree, "SteamLibrary", "steamapps", "common", "Age of Wonders");
            string sibling = Path.Combine(tree, "SteamLibrary", "steamapps", "common", "Age of Wonders zig");
            string nested = Path.Combine(steamCopy, "Age of Wonders darkolord");
            string deepNested = Path.Combine(sibling, "AoW Evolved_0001", "AoW Evolved");
            string rootCopy = Path.Combine(tree, "Age of Wonders");
            string tooDeepAndUnrelated = Path.Combine(tree, "a", "b", "c", "d", "e");
            foreach (string folder in new[] { steamCopy, sibling, nested, deepNested, rootCopy, tooDeepAndUnrelated })
            {
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, AowGame.Aow1ExeName), new byte[] { 1 });
            }
            Directory.CreateDirectory(Path.Combine(tree, "Windows", "System32"));
            File.WriteAllBytes(Path.Combine(tree, "Windows", "System32", AowGame.Aow1ExeName), new byte[] { 1 });

            List<string> found = GameDetector.ScanTree(tree).Select(game => game.Folder).ToList();

            Assert.Contains(rootCopy, found);
            Assert.Contains(steamCopy, found);
            Assert.Contains(sibling, found);
            Assert.Contains(nested, found);
            Assert.Contains(deepNested, found);
            Assert.DoesNotContain(tooDeepAndUnrelated, found);
            Assert.DoesNotContain(Path.Combine(tree, "Windows", "System32"), found);
            Assert.All(found, folder => Assert.True(File.Exists(Path.Combine(folder, AowGame.Aow1ExeName))));
        }

        [Fact]
        public void Detect_RechecksRememberedCopiesWithoutADeepScan()
        {
            GamesConfigValues config = new GamesConfigValues();
            config.Installs.Add(new GameInstallConfigValues { GameType = AowGameType.AowSm, Folder = _zig, Source = InstallSource.Folder });
            config.Installs.Add(new GameInstallConfigValues { GameType = AowGameType.Aow1, Folder = Path.Combine(_root, "gone"), Source = InstallSource.Folder });

            List<AowGame> found = GameDetector.Detect(config.Installs, false);

            AowGame zig = Assert.Single(found, game => game.IsFolder(_zig) && game.GameType == AowGameType.AowSm);
            Assert.Equal(InstallSource.Folder, zig.Source);
            Assert.DoesNotContain(found, game => game.Folder.EndsWith("gone"));
        }

        [Theory]
        [InlineData("Ziggurat", "ziggurat", true)]
        [InlineData("Zig Mod", "zig-mod", true)]
        [InlineData("AoWx", "aowx", true)]
        [InlineData("Vanilla", "AoWx", false)]
        [InlineData("", "", true)]
        [InlineData(null, "", true)]
        public void Labels_CompareWithoutCaseSpacesOrPunctuation(string a, string b, bool same)
        {
            Assert.Equal(same, AowGame.SameLabel(a, b));
        }

        [Fact]
        public void DisplayNameAndIdIncludeTheLabelAndFolder()
        {
            AowGame game = new AowGame(AowGameType.AowSm, _zig, InstallSource.Manual);
            Assert.Equal("Age of Wonders Shadow Magic", game.DisplayName);
            game.Label = "  Ziggurat ";
            Assert.Equal("Age of Wonders Shadow Magic (Ziggurat)", game.DisplayName);
            Assert.True(game.IsManual);
            Assert.Equal("Age of Wonders Shadow Magic", game.GameName);

            AowGame mpe = new AowGame(AowGameType.AowMpe, _zig, InstallSource.Folder);
            Assert.Equal("AoW - MP Evolution", mpe.DisplayName);
            Assert.Equal(game.GameName, mpe.GameName);
            Assert.NotEqual(game.Id, mpe.Id);
            Assert.True(game.IsFolder(_zig.ToUpperInvariant() + "\\"));
        }

        [Fact]
        public void ModHeader_RoundTripsThroughTheMessage()
        {
            MimeMessage message = new MimeMessage();
            Assert.Null(MailHelper.GetModLabel(message));

            MailHelper.SetModLabel(message, " Ziggurat ");
            Assert.Equal("Ziggurat", MailHelper.GetModLabel(message));
            Assert.Equal("Ziggurat", message.Headers[MailHelper.ModHeaderName]);

            MailHelper.SetModLabel(message, "AoWx");
            Assert.Equal("AoWx", MailHelper.GetModLabel(message));
            Assert.Single(message.Headers.Where(header => header.Field == MailHelper.ModHeaderName));

            MailHelper.SetModLabel(message, "");
            Assert.Null(MailHelper.GetModLabel(message));
        }

        [Fact]
        public void GamesConfig_SurvivesTheConfigFile()
        {
            Config config = new Config(true);
            config.GamesConfig.Installs.Add(new GameInstallConfigValues { GameType = AowGameType.AowSm, Folder = _zig, Label = "Ziggurat", Manual = true });

            XmlSerializer serializer = new XmlSerializer(typeof(Config));
            Config loaded;
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, config);
                using (StringReader reader = new StringReader(writer.ToString()))
                {
                    loaded = (Config)serializer.Deserialize(reader);
                }
            }

            GameInstallConfigValues install = Assert.Single(loaded.GamesConfig.Installs);
            Assert.Equal("Ziggurat", install.Label);
            Assert.True(install.Manual);
            Assert.Equal(_zig, Assert.Single(loaded.GamesConfig.ManualFolders));

            //A config written before the Games tab existed has no games element
            Config old;
            using (StringReader reader = new StringReader("<aowemailwrapper_config />"))
            {
                old = (Config)serializer.Deserialize(reader);
            }
            Assert.NotNull(old.GamesConfig);
            Assert.Empty(old.GamesConfig.Installs);
        }
    }
}
