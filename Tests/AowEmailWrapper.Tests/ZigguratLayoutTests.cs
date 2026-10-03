using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Games;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// The Ziggurat installer (2026) builds the mod into a Ziggurat subfolder of the game folder, with its
    /// own AoWz.exe, text tables, Save folder and README, and registers "Age of Wonders Z" as its registry
    /// name. The game folder it was pointed at stays the vanilla game. Layout taken from a real install of
    /// Ziggurat Setup 2026.09.18 into a copy of the Steam game.
    /// </summary>
    public class ZigguratLayoutTests : IDisposable
    {
        private readonly string _root;

        public ZigguratLayoutTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "AowEmailWrapper.Tests", "zig-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        /// <summary>A vanilla game folder; with zigguratTables the text tables are Ziggurat's, as on the mod author's own copy.</summary>
        private string HostFolder(bool zigguratTables)
        {
            string folder = Path.Combine(_root, "Age of Wonders");
            Directory.CreateDirectory(Path.Combine(folder, "Dict"));
            Directory.CreateDirectory(Path.Combine(folder, "Save"));
            File.WriteAllText(Path.Combine(folder, AowGame.Aow1ExeName), "Copyright (C) 1999,2000 Triumph Studios");
            File.WriteAllText(Path.Combine(folder, "Dict", "ResStr.txt"), zigguratTables ? "[US] = [Version: Ziggurat %s]" : "NATIVE = [Version: %s]");
            return folder;
        }

        /// <summary>What the installer leaves under Ziggurat\: the mod's executable, a README with a release date, tables and saves.</summary>
        private static string ZigguratFolder(string host)
        {
            string folder = Path.Combine(host, ModDetector.ZigguratSubfolder);
            Directory.CreateDirectory(Path.Combine(folder, "Dict"));
            Directory.CreateDirectory(Path.Combine(folder, "Save"));
            File.WriteAllText(Path.Combine(folder, AowGame.Aow1ZExeName), "Copyright (C) 1999,2000 Triumph Studios");
            File.WriteAllText(Path.Combine(folder, "AoWzEd.exe"), "editor");
            File.WriteAllText(Path.Combine(folder, "README.txt"), "ZIGGURAT - a total rebalance mod for Age of Wonders 1\nRelease 2026-09-18\n");
            File.WriteAllText(Path.Combine(folder, "Dict", "ResStr.txt"), "NATIVE = [Version: %s]\n[US] = [Version: Ziggurat %s]");
            return folder;
        }

        [Fact]
        public void The_subfolder_is_a_Ziggurat_copy_with_its_own_registry_name()
        {
            string zig = ZigguratFolder(HostFolder(false));

            AowGame game = new AowGame(AowGameType.Aow1, zig, InstallSource.Registry);
            Assert.True(game.IsInstalled);
            Assert.Equal(AowGame.Aow1ZExeName, game.ExeFile);
            Assert.True(game.RunsModExecutable);
            Assert.Equal(AowGame.Aow1ZGameName, game.GameName);

            ModInfo mod = Assert.Single(game.DetectedMods);
            Assert.Equal(ModDetector.Ziggurat, mod.Name);
            Assert.Equal("2026-09-18", mod.Version);
            Assert.Contains(AowGame.Aow1ZExeName, mod.Evidence);
        }

        [Fact]
        public void The_executable_alone_marks_Ziggurat_even_without_its_text_tables()
        {
            string folder = Path.Combine(_root, "bare");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, AowGame.Aow1ZExeName), "x");

            List<ModInfo> mods = ModDetector.Detect(folder);
            Assert.Equal(ModDetector.Ziggurat, Assert.Single(mods).Name);
        }

        [Fact]
        public void The_vanilla_host_keeps_the_stock_registry_name()
        {
            string host = HostFolder(false);
            ZigguratFolder(host);

            AowGame game = new AowGame(AowGameType.Aow1, host, InstallSource.Folder);
            Assert.Equal(AowGame.Aow1ExeName, game.ExeFile);
            Assert.False(game.RunsModExecutable);
            Assert.Equal(AowGame.Aow1GameName, game.GameName);
            Assert.Empty(game.DetectedMods);
        }

        [Fact]
        public void Pointing_at_the_game_folder_finds_the_Ziggurat_copy_inside_it_too()
        {
            string host = HostFolder(false);
            string zig = ZigguratFolder(host);

            List<AowGame> found = GameDetector.ScanFolder(host, InstallSource.Manual);

            Assert.Equal(2, found.Count);
            Assert.Contains(found, game => game.IsFolder(host) && game.ExeFile == AowGame.Aow1ExeName);
            Assert.Contains(found, game => game.IsFolder(zig) && game.ExeFile == AowGame.Aow1ZExeName);
        }

        [Fact]
        public void The_Ziggurat_copy_takes_the_label_and_the_host_is_vanilla()
        {
            string host = HostFolder(false);
            string zig = ZigguratFolder(host);

            AowGameManager manager = new AowGameManager(_root, new[]
            {
                new AowGame(AowGameType.Aow1, host, InstallSource.Steam),
                new AowGame(AowGameType.Aow1, zig, InstallSource.Registry),
            }, null);

            List<AowGame> installs = manager.GetInstalls(AowGameType.Aow1);
            Assert.Equal(ModDetector.Vanilla, installs.Single(game => game.IsFolder(host)).Label);
            Assert.Equal(ModDetector.Ziggurat, installs.Single(game => game.IsFolder(zig)).Label);
        }

        [Fact]
        public void Ziggurat_turns_go_to_the_mods_own_copy_not_a_host_that_only_carries_its_tables()
        {
            //The mod author's game folder has Ziggurat's text tables in it, so it is labelled Ziggurat too; the
            //built subfolder, started through AoWz.exe, is still where Ziggurat turns go
            string host = HostFolder(true);
            string zig = ZigguratFolder(host);

            AowGameManager manager = new AowGameManager(_root, new[]
            {
                new AowGame(AowGameType.Aow1, host, InstallSource.Folder),
                new AowGame(AowGameType.Aow1, zig, InstallSource.Folder),
            }, null);

            List<AowGame> installs = manager.GetInstalls(AowGameType.Aow1);
            AowGame mod = installs.Single(game => game.IsFolder(zig));
            AowGame vanilla = installs.Single(game => game.IsFolder(host));
            Assert.Equal(ModDetector.Ziggurat, mod.Label);
            Assert.Equal(ModDetector.Ziggurat, vanilla.Label);
            Assert.Same(mod, manager.DefaultFor(AowGameType.Aow1, ModDetector.Ziggurat));
            Assert.Same(mod, manager.ResolveIncoming(AowGameType.Aow1, "Ziggurat", "new game.asg"));
        }

        [Fact]
        public void The_menu_lists_the_copy_a_label_goes_to_first_and_under_the_plain_name()
        {
            //As on the PC that showed the problem: an older in-place Ziggurat copy whose folder sorts before the
            //real install, plus an Evolved copy; the real install is the game's default
            string older = Path.Combine(_root, "Age of Wonders zig");
            Directory.CreateDirectory(Path.Combine(older, "Dict"));
            File.WriteAllText(Path.Combine(older, AowGame.Aow1ExeName), "Copyright (C) 1999,2000 Triumph Studios");
            File.WriteAllText(Path.Combine(older, "Dict", "ResStr.txt"), "[US] = [Version: Ziggurat %s]");
            string host = HostFolder(false);
            File.WriteAllText(Path.Combine(host, "Dict", "ResStr.txt"), "[US] = [Version: Evolved %s]");
            string real = ZigguratFolder(host);

            GamesConfigValues config = new GamesConfigValues();
            config.Installs.Add(new GameInstallConfigValues { GameType = AowGameType.Aow1, Folder = real, IsDefault = true, Source = InstallSource.Registry });
            AowGameManager manager = new AowGameManager(_root, new[]
            {
                new AowGame(AowGameType.Aow1, older, InstallSource.Steam),
                new AowGame(AowGameType.Aow1, host, InstallSource.Registry),
                new AowGame(AowGameType.Aow1, real, InstallSource.Registry),
            }, config);

            List<AowGame> inOrder = AowGameManager.InMenuOrder(manager.Games);
            Assert.Equal(new[] { real, host, older }, inOrder.Select(game => game.Folder.TrimEnd(Path.DirectorySeparatorChar)));
            Assert.Equal(new[] { "Age of Wonders (Ziggurat)", "Age of Wonders (Evolved)", "Age of Wonders (Ziggurat, Age of Wonders zig)" },
                inOrder.Select(game => AowGameManager.MenuName(manager.Games, game)));
        }

        [Fact]
        public void An_older_Ziggurat_copy_found_first_does_not_take_the_turns_from_the_real_install()
        {
            //As on a PC where Ziggurat was once installed in place (AoW.exe with Ziggurat tables) and later with
            //the installer (its own AoWz.exe). The older copy is scanned first and the real one is the default
            string older = Path.Combine(_root, "Age of Wonders zig");
            Directory.CreateDirectory(Path.Combine(older, "Dict"));
            File.WriteAllText(Path.Combine(older, AowGame.Aow1ExeName), "Copyright (C) 1999,2000 Triumph Studios");
            File.WriteAllText(Path.Combine(older, "Dict", "ResStr.txt"), "[US] = [Version: Ziggurat %s]");
            string real = ZigguratFolder(HostFolder(false));

            GamesConfigValues config = new GamesConfigValues();
            config.Installs.Add(new GameInstallConfigValues { GameType = AowGameType.Aow1, Folder = real, IsDefault = true, Source = InstallSource.Registry });
            AowGameManager manager = new AowGameManager(_root, new[]
            {
                new AowGame(AowGameType.Aow1, older, InstallSource.Steam),
                new AowGame(AowGameType.Aow1, real, InstallSource.Registry),
            }, config);

            List<AowGame> installs = manager.GetInstalls(AowGameType.Aow1);
            AowGame realCopy = installs.Single(game => game.IsFolder(real));
            AowGame olderCopy = installs.Single(game => game.IsFolder(older));
            Assert.Equal("Ziggurat", olderCopy.Label);
            Assert.Equal("Ziggurat", realCopy.Label);
            Assert.True(realCopy.IsDefault);
            Assert.Same(realCopy, manager.ResolveIncoming(AowGameType.Aow1, "Ziggurat", "Heulax 26.asg"));

            //Even without the default, the copy with the mod's own executable wins over the older one
            config.Installs[0].IsDefault = false;
            manager = new AowGameManager(_root, new[]
            {
                new AowGame(AowGameType.Aow1, older, InstallSource.Registry),
                new AowGame(AowGameType.Aow1, real, InstallSource.Steam),
            }, config);
            Assert.Same(manager.GetInstalls(AowGameType.Aow1).Single(game => game.IsFolder(real)), manager.ResolveIncoming(AowGameType.Aow1, "Ziggurat", "Heulax 26.asg"));
        }

        [Fact]
        public void A_Ziggurat_copy_and_its_turns_show_the_purple_dragon_and_a_vanilla_host_does_not()
        {
            //A plain game folder beside the mod's own subfolder
            string host = HostFolder(false);
            string zig = ZigguratFolder(host);

            AowGameManager manager = new AowGameManager(_root, new[]
            {
                new AowGame(AowGameType.Aow1, host, InstallSource.Folder),
                new AowGame(AowGameType.Aow1, zig, InstallSource.Folder),
            }, null);

            List<AowGame> installs = manager.GetInstalls(AowGameType.Aow1);
            Assert.Equal(AowGame.ZigguratIcon, installs.Single(game => game.IsFolder(zig)).ImageKey);
            Assert.Equal(AowGameType.Aow1.ToString(), installs.Single(game => game.IsFolder(host)).ImageKey);

            ConfigFramework.Activity inZig = new ConfigFramework.Activity(ConfigFramework.ActivityState.Sent, AowGameType.Aow1, "a.asg", "Map", "1") { InstallFolder = zig };
            ConfigFramework.Activity inHost = new ConfigFramework.Activity(ConfigFramework.ActivityState.Sent, AowGameType.Aow1, "b.asg", "Map", "1") { InstallFolder = host };
            ConfigFramework.Activity labelOnly = new ConfigFramework.Activity(ConfigFramework.ActivityState.Received, AowGameType.Aow1, "c.asg", "Map", "1") { ModLabel = "zig gurat" };
            Assert.Equal(AowGame.ZigguratIcon, manager.ImageKeyFor(inZig));
            Assert.Equal(AowGameType.Aow1.ToString(), manager.ImageKeyFor(inHost));
            Assert.Equal(AowGame.ZigguratIcon, manager.ImageKeyFor(labelOnly));
        }

        [Theory]
        [InlineData(AowGameType.Aow1, "Ziggurat", AowGame.ZigguratIcon)]
        [InlineData(AowGameType.Aow1, "Vanilla 1.36", "Aow1")]
        [InlineData(AowGameType.Aow1, "Dark Lord", "Aow1")]
        [InlineData(AowGameType.Aow1, null, "Aow1")]
        [InlineData(AowGameType.AowSm, "Ziggurat", "AowSm")]
        public void Only_an_Age_of_Wonders_1_label_of_Ziggurat_picks_the_purple_dragon(AowGameType type, string label, string key)
        {
            Assert.Equal(key, AowGame.ImageKeyFor(type, label));
        }
    }
}
