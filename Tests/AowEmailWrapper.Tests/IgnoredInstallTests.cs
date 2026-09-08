using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Games;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// Removing a detected copy on the Games tab must stick: detection finds the folder again on every
    /// start, so the config remembers it as one to leave out until the folder is added by hand.
    /// </summary>
    public class IgnoredInstallTests : IDisposable
    {
        private readonly string _root;

        public IgnoredInstallTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "AowEmailWrapper.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        private string GameFolder(string name)
        {
            string folder = Path.Combine(_root, name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "AoW.exe"), "Copyright (C) 1999,2000 Triumph Studios");
            return folder;
        }

        [Fact]
        public void An_ignored_copy_is_left_out_of_detection_and_the_default_moves_on()
        {
            string kept = GameFolder("kept");
            string removed = GameFolder("removed");
            AowGame keptGame = new AowGame(AowGameType.Aow1, kept, InstallSource.Folder);
            AowGame removedGame = new AowGame(AowGameType.Aow1, removed, InstallSource.Folder);

            GamesConfigValues config = new GamesConfigValues();
            config.Installs.Add(new GameInstallConfigValues(removedGame) { IsDefault = true });
            config.Ignore(removedGame);

            AowGameManager manager = new AowGameManager(_root, new[] { keptGame, removedGame }, config);

            AowGame only = Assert.Single(manager.GetInstalls(AowGameType.Aow1));
            Assert.True(only.IsFolder(kept));
            Assert.True(only.IsDefault);
            Assert.Single(manager.IgnoredInstalls);
            Assert.True(manager.ToConfig().IsIgnored(removedGame));
        }

        [Fact]
        public void Adding_the_folder_by_hand_forgets_the_ignore()
        {
            string folder = GameFolder("back");
            AowGame game = new AowGame(AowGameType.Aow1, folder, InstallSource.Folder);
            GamesConfigValues config = new GamesConfigValues();
            config.Ignore(game);
            config.Ignore(game);
            Assert.Single(config.Ignored);

            config.Unignore(folder.ToUpperInvariant() + Path.DirectorySeparatorChar);
            Assert.Empty(config.Ignored);
            Assert.False(config.IsIgnored(game));
        }

        [Fact]
        public void Ignored_copies_survive_the_config_xml_and_a_clone()
        {
            string folder = GameFolder("saved");
            GamesConfigValues config = new GamesConfigValues();
            config.Ignore(new AowGame(AowGameType.AowSm, folder, InstallSource.Registry));

            XmlSerializer serializer = new XmlSerializer(typeof(GamesConfigValues));
            string xml;
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, config);
                xml = writer.ToString();
            }
            Assert.Contains("<ignore", xml);

            GamesConfigValues back;
            using (StringReader reader = new StringReader(xml))
            {
                back = (GamesConfigValues)serializer.Deserialize(reader);
            }
            IgnoredInstallConfigValues entry = Assert.Single(back.Ignored);
            Assert.Equal(AowGameType.AowSm, entry.GameType);
            Assert.True(AowGame.SameFolder(folder, entry.Folder));
            Assert.Single(back.Clone().Ignored);
        }
    }
}
