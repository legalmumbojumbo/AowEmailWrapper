using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace AowEmailWrapper.ConfigFramework
{
    [XmlRoot("accounts")]
    public class AccountConfigValuesList
    {
        private List<AccountConfigValues> _accounts;
        private string _activeAccountName;
        private string _startUpAccountName;

        [XmlElement("account")]
        public List<AccountConfigValues> Accounts
        {
            get { return _accounts; }
            set { _accounts = value; }
        }

        [XmlAttribute("active_account")]
        public string ActiveAccountName
        {
            get { return _activeAccountName; }
            set { _activeAccountName = value; }
        }

        [XmlAttribute("startup_account")]
        public string StartUpAccountName
        {
            get { return _startUpAccountName; }
            set { _startUpAccountName = value; }
        }

        [XmlIgnore]
        public AccountConfigValues ActiveAccount
        {
            get { return GetAccountByName(_activeAccountName); }
        }

        [XmlIgnore]
        public AccountConfigValues StartUpAccount
        {
            //Falls back onto the active account name should this be a new config or one from a previous build of the Wrapper
            get
            {
                if (string.IsNullOrEmpty(_startUpAccountName))
                {
                    _startUpAccountName = _activeAccountName;
                }
                return GetAccountByName(_startUpAccountName);
            }
        }

        /// <summary>Accounts that check for email; each also sends the replies for the games it received.</summary>
        [XmlIgnore]
        public List<AccountConfigValues> ActiveAccounts
        {
            get { return _accounts == null ? new List<AccountConfigValues>() : _accounts.FindAll(account => account.IsActive); }
        }

        /// <summary>
        /// The account games you start yourself send from and the one the games are pointed at:
        /// the first active account in the list, or the first account when none is active.
        /// </summary>
        [XmlIgnore]
        public AccountConfigValues PrimaryAccount
        {
            get
            {
                if (_accounts == null || _accounts.Count == 0)
                {
                    return null;
                }
                return _accounts.Find(account => account.IsActive) ?? _accounts[0];
            }
        }

        public AccountConfigValues GetAccountByName(string name)
        {
            return _accounts.Find(item => !string.IsNullOrEmpty(item.Name) && item.Name.Equals(name, StringComparison.InvariantCulture));
        }

        public bool CheckAccountExistsByName(string name)
        {
            return (GetAccountByName(name) != null);
        }

        /*
        public string GetEmailProviderType(string input)
        {
            string returnVal = null;

            if (_accounts != null &&
                _accounts.Count > 0 &&
                _accounts.Find(account => account.TemplateDomains != null) != null)
            {
                AccountConfigValues templateMatch = _accounts.Find(item => item.IsDomainMatch(input));
                if (templateMatch != null)
                {
                    returnVal = templateMatch.EmailProvider;
                }
            }

            return returnVal;
        }
        */

        public AccountConfigValuesList()
        { }
    }
}
