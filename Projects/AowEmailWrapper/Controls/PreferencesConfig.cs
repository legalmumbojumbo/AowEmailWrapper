using System;
using System.Windows.Forms;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Helpers;
using AowEmailWrapper.Localization;

namespace AowEmailWrapper.Controls
{
    public partial class PreferencesConfig : UserControl
    {
        public EventHandler Config_Changed;
        /// <summary>Raised when the Look combo changes, so the window can restyle itself at once.</summary>
        public event EventHandler ThemeChanged;
        private const string EmailInSelectedMessageKey = "msgEmailInSelected";
        private const string SaveSelectedMessageKey = "msgSaveSelected";
        private const string ThemeClassicKey = "themeClassic";
        private const string ThemeAgeOfWondersKey = "themeAgeOfWonders";
        private const string ThemeAgeOfWondersWhiteKey = "themeAgeOfWondersWhite";
        /// <summary>The note on the name sent with turns, shared with the account wizard's.</summary>
        private const string PlayerNameHintKey = "lblPlayerNameHint";
        private PreferencesConfigValues _config;
        private bool _populating;

        public PreferencesConfig()
        {
            InitializeComponent();

            EventHandler raiseConfigChange = new EventHandler(Raise_Config_Changed);
            foreach (EmailSaveFolder value in Enum.GetValues(typeof(EmailSaveFolder)))
            {
                fbSaveFolder.AddItem(value.ToString(), Translator.TranslateEnum(value));
            }

            if (Translator.ComboBoxItems != null && Translator.ComboBoxItems.Count > 0)
            {
                Translator.ComboBoxItems.ForEach(item => fbLocalization.AddItem(item));
            }

            fbTheme.AddItem(Theme.ClassicName, Translator.Translate(ThemeClassicKey));
            fbTheme.AddItem(Theme.AgeOfWondersName, Translator.Translate(ThemeAgeOfWondersKey));
            fbTheme.AddItem(Theme.AgeOfWondersWhiteName, Translator.Translate(ThemeAgeOfWondersWhiteKey));
            fbTheme.SelectedValue = Theme.DefaultName;

            fbSaveFolder.SelectedIndex = 0;

            fbEmailSound.InnerCheckBox.CheckedChanged += raiseConfigChange;
            fbSentSound.InnerCheckBox.CheckedChanged += raiseConfigChange;
            fbAutostart.InnerCheckBox.CheckedChanged += raiseConfigChange;
            fbAutoInstallUpdates.InnerCheckBox.CheckedChanged += raiseConfigChange;
            fbSaveFolder.InnerComboBox.SelectedIndexChanged += raiseConfigChange;
            fbSaveFolder.InnerComboBox.SelectedIndexChanged += new EventHandler(SaveFolder_SelectedIndexChanged);
            fbCopyToEmailOut.InnerCheckBox.CheckedChanged += raiseConfigChange;
            fbTellPlayers.InnerCheckBox.CheckedChanged += raiseConfigChange;
            fbLocalization.InnerComboBox.SelectedIndexChanged += raiseConfigChange;
            fbTheme.InnerComboBox.SelectedIndexChanged += raiseConfigChange;
            fbTheme.InnerComboBox.SelectedIndexChanged += (sender, e) => { if (!_populating) ThemeChanged?.Invoke(this, e); };
            fbGameWrapperDataPort.InnerTextBox.TextChanged += raiseConfigChange;
            fbPlayerName.InnerTextBox.MaxLength = PlayerAlias.MaxNameLength;
            string playerNameHint = Translator.Translate(PlayerNameHintKey);
            fbPlayerName.InnerTextBox.PlaceholderText = playerNameHint;
            new ToolTip().SetToolTip(fbPlayerName.InnerTextBox, playerNameHint);
            fbPlayerName.InnerTextBox.TextChanged += raiseConfigChange;
        }

        /// <summary>Fills in the name sent with the player's turns, unless they have given one already.</summary>
        public void OfferPlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(fbPlayerName.TextValue) && !string.IsNullOrWhiteSpace(name))
            {
                fbPlayerName.TextValue = name.Trim();
            }
        }

        public string Prefix
        {
            get { return "Preferences"; }
        }

        public PreferencesConfigValues Config
        {
            get
            {
                Scrape();
                return _config;
            }
            set
            {
                _config = value;
                Populate();
            }
        }

        private void Scrape()
        {
            _config = new PreferencesConfigValues();

            _config.PlaySoundOnEmail = fbEmailSound.Checked;
            _config.PlaySoundOnSend = fbSentSound.Checked;
            _config.Autostart = fbAutostart.Checked;
            _config.AutoInstallUpdates = fbAutoInstallUpdates.Checked;
            _config.SaveFolder = ConfigHelper.ParseEnumString<EmailSaveFolder>(fbSaveFolder.SelectedValue);
            _config.CopyToEmailOut = fbCopyToEmailOut.Checked;
            _config.TellPlayers = fbTellPlayers.Checked;
            _config.Theme = !string.IsNullOrEmpty(fbTheme.SelectedValue) ? fbTheme.SelectedValue : Theme.DefaultName;
            _config.LanguageCode = !string.IsNullOrEmpty(fbLocalization.SelectedValue) ? fbLocalization.SelectedValue : Translator.CurrentLanguageCode;
            _config.PlayerName = string.IsNullOrWhiteSpace(fbPlayerName.TextValue) ? null : fbPlayerName.TextValue.Trim();

            int testValue;
            if (int.TryParse(fbGameWrapperDataPort.TextValue, out testValue))
            {
                _config.GameWrapperDataPort = IsUnassignedPortRange(testValue) ? testValue : PreferencesConfigValues.GameWrapperDataPortDefault;
                fbGameWrapperDataPort.TextValue = _config.GameWrapperDataPort.ToString();
            }
            else
            {
                _config.GameWrapperDataPort = PreferencesConfigValues.GameWrapperDataPortDefault;
            }
        }

        private void Populate()
        {
            _populating = true;
            try
            {
                fbEmailSound.Checked = _config.PlaySoundOnEmail;
                fbSentSound.Checked = _config.PlaySoundOnSend;
                fbAutostart.Checked = _config.Autostart;
                fbAutoInstallUpdates.Checked = _config.AutoInstallUpdates;
                fbSaveFolder.SelectedValue = _config.SaveFolder.ToString();
                fbCopyToEmailOut.Checked = _config.CopyToEmailOut;
                fbTellPlayers.Checked = _config.TellPlayers;
                fbLocalization.SelectedValue = _config.LanguageCode;
                fbTheme.SelectedValue = Theme.Normalize(_config.Theme);
                fbPlayerName.TextValue = _config.PlayerName ?? string.Empty;
                fbGameWrapperDataPort.TextValue = _config.GameWrapperDataPort.ToString();
                UpdateSaveFolderTip();
            }
            finally
            {
                _populating = false;
            }
        }

        private void Raise_Config_Changed(object sender, EventArgs e)
        {
            if (Config_Changed != null)
            {
                Config_Changed(this, e);
            }
        }

        private void SaveFolder_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSaveFolderTip();
        }

        private void UpdateSaveFolderTip()
        {
            switch (ConfigHelper.ParseEnumString<EmailSaveFolder>(fbSaveFolder.SelectedValue))
            {
                case EmailSaveFolder.EmailIn:
                    labelMessage.Text = Translator.Translate(EmailInSelectedMessageKey);
                    break;
                case EmailSaveFolder.Save:
                    labelMessage.Text = Translator.Translate(SaveSelectedMessageKey);
                    break;
            }
        }

        private bool IsUnassignedPortRange(int input)
        {
            return (input >= 49151 && input < 65535);
        }
    }
}
