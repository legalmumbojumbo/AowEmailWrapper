using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AowEmailWrapper.Games;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// Mods are recognised from what they leave in a game folder. The markers were taken from the
    /// mods' own installers and checked against real copies on disk.
    /// </summary>
    public class ModDetectorTests : IDisposable
    {
        private readonly string _root;

        public ModDetectorTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "AowEmailWrapper.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        [Fact]
        public void A_stock_copy_has_no_mods()
        {
            string folder = GameFolder("stock", resStrVersion: "Version: Evolved %s");

            Assert.Empty(ModDetector.Detect(folder));
            Assert.Empty(ModDetector.Detect(Path.Combine(_root, "missing")));
            Assert.Empty(ModDetector.Detect(null));
        }

        [Fact]
        public void Ziggurat_is_recognised_from_its_version_string_and_dated_by_its_readme()
        {
            string folder = GameFolder("zig", resStrVersion: "Version: Ziggurat %s");
            File.WriteAllText(Path.Combine(folder, "README.txt"), "ZIGGURAT \u2014 a total rebalance mod for Age of Wonders 1\r\nRelease 2026-09-08\r\n\r\nINSTALL\r\n", Encoding.Latin1);

            ModInfo mod = Assert.Single(ModDetector.Detect(folder));
            Assert.Equal(ModDetector.Ziggurat, mod.Name);
            Assert.Equal("2026-09-08", mod.Version);
            Assert.Equal("Ziggurat 2026-09-08", mod.ToString());
            Assert.Contains("ResStr.txt", mod.Evidence);
        }

        [Fact]
        public void An_older_Ziggurat_without_a_readme_is_still_recognised()
        {
            //The stock game's own readme must not be mistaken for the mod's
            string folder = GameFolder("oldzig", resStrVersion: "Version: Ziggurat %s");
            File.WriteAllText(Path.Combine(folder, "readme.txt"), "IPXWrapper README\r\n");

            ModInfo mod = Assert.Single(ModDetector.Detect(folder));
            Assert.Equal(ModDetector.Ziggurat, mod.Name);
            Assert.Null(mod.Version);
            Assert.Equal("Ziggurat", mod.ToString());
        }

        [Theory]
        [InlineData("ZIGGURAT - a total rebalance mod\nRelease 2026-09-08", "2026-09-08")]
        [InlineData("  ziggurat notes\r\n\r\nrelease 2025-01-31 fixes", "2025-01-31")]
        [InlineData("ZIGGURAT\nno date here", null)]
        [InlineData("IPXWrapper README\nRelease 2026-09-08", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void The_readme_release_date_is_only_read_from_a_Ziggurat_readme(string text, string expected)
        {
            Assert.Equal(expected, ModDetector.ZigguratRelease(text));
        }

        [Fact]
        public void AoWx_is_recognised_from_the_branded_executable()
        {
            string folder = GameFolder("aowx", resStrVersion: "Version: Evolved %s");
            WriteExe(Path.Combine(folder, "AoW.exe"), "Age of Wonders X mod by IniochReborn");

            ModInfo mod = Assert.Single(ModDetector.Detect(folder));
            Assert.Equal(ModDetector.AowX, mod.Name);
            Assert.Contains("AoW.exe", mod.Evidence);
        }

        [Fact]
        public void A_stock_executable_is_not_AoWx()
        {
            string folder = GameFolder("stockexe", resStrVersion: "Version: Evolved %s");
            WriteExe(Path.Combine(folder, "AoW.exe"), "Copyright (C) 1999,2000 Triumph Studios");

            Assert.Empty(ModDetector.Detect(folder));
        }

        [Fact]
        public void A_folder_with_both_mods_reports_both()
        {
            //A Ziggurat data set launched through an AoWx executable, as one real copy on this machine is
            string folder = GameFolder("mixed", resStrVersion: "Version: Ziggurat %s");
            WriteExe(Path.Combine(folder, "AoWx.exe"), "Age of Wonders X mod by IniochReborn");
            File.WriteAllText(Path.Combine(folder, "readme_DarkLordMod.txt"), "Dark Lord");

            List<string> names = ModDetector.Detect(folder).Select(mod => mod.Name).ToList();
            Assert.Equal(new[] { ModDetector.Ziggurat, ModDetector.AowX, ModDetector.DarkLord }, names);
        }

        [Fact]
        public void An_AoWx_copy_saves_into_its_X_subfolder()
        {
            string folder = GameFolder("aowxsave", resStrVersion: "Version: Evolved %s");
            WriteExe(Path.Combine(folder, "AoW.exe"), "Age of Wonders X mod by IniochReborn");
            Directory.CreateDirectory(Path.Combine(folder, "Save"));
            Directory.CreateDirectory(Path.Combine(folder, "X", "Save"));

            AowGame game = new AowGame(AowGameType.Aow1, folder, InstallSource.Manual);

            Assert.True(game.IsInstalled);
            Assert.Equal("AoWx", game.DetectedModNames);
            Assert.Equal(Path.Combine(folder, "X", "Save"), game.Save.FullName);
            Assert.Contains(game.TurnFolders, turnFolder => turnFolder.FullName == game.Save.FullName);
        }

        [Fact]
        public void A_newly_found_modded_copy_is_labelled_and_a_second_one_is_not()
        {
            string first = GameFolder("label1", resStrVersion: "Version: Ziggurat %s");
            string second = GameFolder("label2", resStrVersion: "Version: Ziggurat %s");
            string stock = GameFolder("label3", resStrVersion: "Version: Evolved %s");
            foreach (string folder in new[] { first, second, stock })
            {
                WriteExe(Path.Combine(folder, "AoW.exe"), "Copyright (C) 1999,2000 Triumph Studios");
            }

            AowGameManager manager = new AowGameManager(_root, new[]
            {
                new AowGame(AowGameType.Aow1, first, InstallSource.Folder),
                new AowGame(AowGameType.Aow1, second, InstallSource.Folder),
                new AowGame(AowGameType.Aow1, stock, InstallSource.Folder),
            }, null);

            List<AowGame> installs = manager.GetInstalls(AowGameType.Aow1);
            Assert.Equal("Ziggurat", installs.Single(game => game.IsFolder(first)).Label);
            Assert.Equal(string.Empty, installs.Single(game => game.IsFolder(second)).Label);
            Assert.Equal(string.Empty, installs.Single(game => game.IsFolder(stock)).Label);
        }

        [Fact]
        public void Real_copies_on_this_machine_are_recognised_when_present()
        {
            //Checked by hand against these folders; skipped on a machine without them
            string zigguratCopy = @"E:\Age of Wonders";
            string mixedCopy = @"E:\SteamLibrary\steamapps\common\Age of Wonders zig";
            string stockCopy = @"E:\SteamLibrary\steamapps\common\Age of Wonders";

            if (Directory.Exists(zigguratCopy))
            {
                Assert.Contains(ModDetector.Detect(zigguratCopy), mod => mod.Name == ModDetector.Ziggurat);
            }
            if (Directory.Exists(mixedCopy))
            {
                List<string> names = ModDetector.Detect(mixedCopy).Select(mod => mod.Name).ToList();
                Assert.Contains(ModDetector.Ziggurat, names);
                Assert.Contains(ModDetector.AowX, names);
            }
            if (Directory.Exists(stockCopy))
            {
                Assert.Empty(ModDetector.Detect(stockCopy));
            }

            //A real AoWx install made with the Inno Setup installer: branded AoWx.exe beside a small AoW.exe launcher
            string aowxCopy = @"E:\Age of Wonders X";
            if (Directory.Exists(aowxCopy))
            {
                ModInfo mod = Assert.Single(ModDetector.Detect(aowxCopy));
                Assert.Equal(ModDetector.AowX, mod.Name);
                AowGame game = new AowGame(AowGameType.Aow1, aowxCopy, InstallSource.Folder);
                Assert.True(game.IsInstalled);
                Assert.Equal(Path.Combine(aowxCopy, "X", "Save"), game.Save.FullName);
            }
        }

        private string GameFolder(string name, string resStrVersion)
        {
            string folder = Path.Combine(_root, name);
            Directory.CreateDirectory(Path.Combine(folder, "Dict"));
            string table = "[:12:]\r\nNATIVE    = [Version: %s]\r\n[US]      = [" + resStrVersion + "]\r\n[DE]      = [Version: %s]\r\n";
            File.WriteAllText(Path.Combine(folder, "Dict", "ResStr.txt"), table, Encoding.Latin1);
            return folder;
        }

        private static void WriteExe(string path, string embedded)
        {
            byte[] noise = new byte[4096];
            new Random(3).NextBytes(noise);
            byte[] text = Encoding.ASCII.GetBytes(embedded);
            byte[] data = new byte[noise.Length * 2 + text.Length];
            Buffer.BlockCopy(noise, 0, data, 0, noise.Length);
            Buffer.BlockCopy(text, 0, data, noise.Length, text.Length);
            Buffer.BlockCopy(noise, 0, data, noise.Length + text.Length, noise.Length);
            File.WriteAllBytes(path, data);
        }
    }
}
