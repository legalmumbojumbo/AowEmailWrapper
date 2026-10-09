using System.Globalization;
using System.Xml.Serialization;

namespace AowEmailWrapper.ConfigFramework
{
    public enum EmailSaveFolder
    {
        [XmlEnum(Name = "EmailIn")]
        EmailIn,
        [XmlEnum(Name = "Save")]
        Save
    }

    [XmlRoot("preferences_config")]
    public class PreferencesConfigValues
    {
        public const int GameWrapperDataPortDefault = 49252;

        private bool _playSoundOnEmail;
        private bool _playSoundOnSend;
        private bool _autostart;
        private EmailSaveFolder _saveFolder = EmailSaveFolder.EmailIn;
        private string _languageCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        private bool _copyToEmailOut = false;
        private int _gameWrapperDataPort = GameWrapperDataPortDefault;
        private bool _autoInstallUpdates = true;
        private bool _tellPlayers = true;

        [XmlAttribute("playsoundonemail")]
        public bool PlaySoundOnEmail
        {
            get { return _playSoundOnEmail; }
            set { _playSoundOnEmail = value; }
        }

        [XmlAttribute("playsoundonsend")]
        public bool PlaySoundOnSend
        {
            get { return _playSoundOnSend; }
            set { _playSoundOnSend = value; }
        }

        [XmlAttribute("autostart")]
        public bool Autostart
        {
            get { return _autostart; }
            set { _autostart = value; }
        }

        [XmlAttribute("save_folder")]
        public EmailSaveFolder SaveFolder
        {
            get { return _saveFolder; }
            set { _saveFolder = value; }
        }

        [XmlAttribute("copyToEmailOut")]
        public bool CopyToEmailOut
        {
            get { return _copyToEmailOut; }
            set { _copyToEmailOut = value; }
        }

        /// <summary>
        /// Whether a turn sent also tells the game's other players' Wrappers whom it went to, so theirs always know
        /// where the turn is. On unless switched off, also in settings saved before it existed.
        /// </summary>
        [XmlAttribute("tellPlayers")]
        public bool TellPlayers
        {
            get { return _tellPlayers; }
            set { _tellPlayers = value; }
        }

        [XmlAttribute("languageCode")]
        public string LanguageCode
        {
            get { return _languageCode; }
            set { _languageCode = value; }
        }

        [XmlAttribute("gameWrapperDataPort")]
        public int GameWrapperDataPort
        {
            get { return _gameWrapperDataPort; }
            set { _gameWrapperDataPort = value; }
        }

        /// <summary>Install a newer build found at start-up without asking. Off means the player is told once and installs from the About tab.</summary>
        [XmlAttribute("autoInstallUpdates")]
        public bool AutoInstallUpdates
        {
            get { return _autoInstallUpdates; }
            set { _autoInstallUpdates = value; }
        }

        private string _theme = Helpers.Theme.DefaultName;

        /// <summary>Name of the look: Theme.ClassicName, Theme.AgeOfWondersName or Theme.AgeOfWondersWhiteName.</summary>
        [XmlAttribute("theme")]
        public string Theme
        {
            get { return _theme; }
            set { _theme = string.IsNullOrEmpty(value) ? Helpers.Theme.DefaultName : value; }
        }

        /// <summary>The name the player goes by, sent with their turns so the other players see it instead of the address.</summary>
        [XmlAttribute("playerName")]
        public string PlayerName { get; set; }

        /// <summary>Activity log column widths the player has dragged, as ListViewColumnResizer.SavedWidths gives them.</summary>
        [XmlAttribute("activityColumns")]
        public string ActivityColumnWidths { get; set; }

        /// <summary>Account list column widths the player has dragged, as ListViewColumnResizer.SavedWidths gives them.</summary>
        [XmlAttribute("accountsColumns")]
        public string AccountsColumnWidths { get; set; }

        /// <summary>The size the player last gave the window, "width,height" at 96 dpi; empty for the default size.</summary>
        [XmlAttribute("windowSize")]
        public string WindowSize { get; set; }

        public PreferencesConfigValues()
            : this(false)
        { }

        public PreferencesConfigValues(bool defaults)
        {
            if (defaults)
            {
                _playSoundOnEmail = true;
                _playSoundOnSend = true;
                _autostart = false;
                _saveFolder = EmailSaveFolder.EmailIn;
                _copyToEmailOut = true;
                _languageCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
                _gameWrapperDataPort = GameWrapperDataPortDefault;
                _autoInstallUpdates = true;
                _tellPlayers = true;
            }
        }
    }
}
