using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using AowEmailWrapper.Games;

namespace AowEmailWrapper.ConfigFramework
{
    /// <summary>What the player has told us about one copy of a game: its label and whether it is the default.</summary>
    [XmlRoot("install")]
    public class GameInstallConfigValues
    {
        [XmlAttribute("game_type")]
        public AowGameType GameType { get; set; }

        [XmlAttribute("folder")]
        public string Folder { get; set; }

        [XmlAttribute("label")]
        public string Label { get; set; }

        [XmlAttribute("default")]
        public bool IsDefault { get; set; }

        /// <summary>Added through the Games tab rather than found by detection, so it is kept even when detection would not find it.</summary>
        [XmlAttribute("manual")]
        public bool Manual { get; set; }

        /// <summary>How the copy was found, so a normal start can re-check it without a deep scan.</summary>
        [XmlAttribute("source")]
        public InstallSource Source { get; set; } = InstallSource.Folder;

        public GameInstallConfigValues()
        { }

        public GameInstallConfigValues(AowGame game)
        {
            GameType = game.GameType;
            Folder = game.Folder;
            Label = game.Label;
            IsDefault = game.IsDefault;
            Manual = game.IsManual;
            Source = game.Source;
        }

        public bool Matches(AowGame game)
        {
            return game != null && game.GameType == GameType && game.IsFolder(Folder);
        }
    }

    [XmlRoot("games")]
    public class GamesConfigValues
    {
        private List<GameInstallConfigValues> _installs = new List<GameInstallConfigValues>();

        [XmlElement("install")]
        public List<GameInstallConfigValues> Installs
        {
            get { return _installs; }
            set { _installs = value ?? new List<GameInstallConfigValues>(); }
        }

        public IEnumerable<string> ManualFolders
        {
            get { return _installs.Where(install => install.Manual && !string.IsNullOrEmpty(install.Folder)).Select(install => install.Folder).Distinct(); }
        }

        public GameInstallConfigValues Find(AowGame game)
        {
            return _installs.FirstOrDefault(install => install.Matches(game));
        }

        public GamesConfigValues Clone()
        {
            GamesConfigValues clone = new GamesConfigValues();
            foreach (GameInstallConfigValues install in _installs)
            {
                clone.Installs.Add(new GameInstallConfigValues
                {
                    GameType = install.GameType,
                    Folder = install.Folder,
                    Label = install.Label,
                    IsDefault = install.IsDefault,
                    Manual = install.Manual,
                    Source = install.Source
                });
            }
            return clone;
        }
    }
}
