using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace AowEmailWrapper.ConfigFramework
{
    [XmlRoot("account")]
    public class AccountConfigValues
    {
        private PollingConfigValues _pollingConfigValues;
        private SmtpConfigValues _smtpConfigValues;
        private string _name;
        private string _emailProviderType; //Template property
        private string _shortUserName; //Template property
        private List<string> _templateDomains; //Template property
        private bool _isGuess;

        [XmlElement("polling_config")]
        public PollingConfigValues PollingConfig
        {
            get { return _pollingConfigValues; }
            set { _pollingConfigValues = value; }
        }

        [XmlElement("smtp_config")]
        public SmtpConfigValues SmtpConfig
        {
            get { return _smtpConfigValues; }
            set { _smtpConfigValues = value; }
        }

        [XmlAttribute("name")]
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        //Template property
        [XmlAttribute("emailprovidertype")]
        public string EmailProvider
        {
            get { return _emailProviderType; }
            set { _emailProviderType = value; }
        }

        //Template property
        [XmlElement("domain")]
        public List<string> TemplateDomains
        {
            get { return _templateDomains; }
            set { _templateDomains = value; }
        }

        //Template property
        [XmlAttribute("shortusername")]
        public string ShortUserName
        {
            get { return _shortUserName; }
            set { _shortUserName = value; }
        }

        /// <summary>Empty for password sign-in, otherwise the OAuth provider name (see MicrosoftOAuth.ProviderName).</summary>
        [XmlAttribute("oauthprovider")]
        public string OAuthProvider { get; set; }

        /// <summary>An active account checks for email and sends the replies for the games it received.</summary>
        [XmlIgnore]
        public bool IsActive
        {
            get { return _pollingConfigValues != null && _pollingConfigValues.UsePolling; }
        }

        [XmlIgnore]
        public bool IsGuess
        {
            get { return _isGuess; }
            set { _isGuess = value; }
        }

        /// <summary>True when either chosen server accepts OAuth2 sign-in only, which the Wrapper cannot do yet.</summary>
        [XmlIgnore]
        public bool RequiresOAuth
        {
            get
            {
                return (_pollingConfigValues != null && _pollingConfigValues.RequiresOAuth) ||
                    (_smtpConfigValues != null && _smtpConfigValues.RequiresOAuth);
            }
        }

        public AccountConfigValues()
        { }

        public bool IsDomainMatch(string emailAddress)
        {
            bool returnVal = false;

            if (_templateDomains != null && _templateDomains.Count > 0 & !string.IsNullOrEmpty(emailAddress))
            {
                string emailAddressTrimmed = emailAddress.ToLower().Trim();
                returnVal = _templateDomains.Find(domain => emailAddressTrimmed.Contains(domain)) != null;
            }

            return returnVal;
        }

    }
}
