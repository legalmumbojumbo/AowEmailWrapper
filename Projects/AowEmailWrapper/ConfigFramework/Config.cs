using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace AowEmailWrapper.ConfigFramework
{
    [XmlRoot("aowemailwrapper_config")]
    public class Config
    {
        private PreferencesConfigValues _preferencesConfigValues;
        private AccountConfigValuesList _accountsList;
        private GamesConfigValues _gamesConfig = new GamesConfigValues();

        [XmlElement("preferences_config")]
        public PreferencesConfigValues PreferencesConfig
        {
            get { return _preferencesConfigValues; }
            set { _preferencesConfigValues = value; }
        }

        [XmlElement("accounts")]
        public AccountConfigValuesList AccountsList
        {
            get { return _accountsList; }
            set { _accountsList = value; }
        }

        /// <summary>Labels and defaults for the installed copies of the games; detection fills in the rest.</summary>
        [XmlElement("games")]
        public GamesConfigValues GamesConfig
        {
            get { return _gamesConfig; }
            set { _gamesConfig = value ?? new GamesConfigValues(); }
        }

        public Config()
            : this(false)
        { }

        public Config(bool defaults)
        {
            if (defaults)
            {
                _preferencesConfigValues = new PreferencesConfigValues(true);
                _accountsList = new AccountConfigValuesList();
                _accountsList.Accounts = new List<AccountConfigValues>();
            }
        }
    }
}
