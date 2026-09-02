using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Mozilla.Autoconfig
{
    public class ServerBase
    {
        private const string EMAILADDRESS = "%EMAILADDRESS%";
        private const string EMAILDOMAIN = "%EMAILDOMAIN%";
        private const string EMAILLOCALPART = "%EMAILLOCALPART%";
        private const char At = '@';
        
        private ServerType _type;
        private string _hostname;
        private int _port;
        private SocketType _socketType;
        private string _usernameFormat;
        private List<string> _authenticationValues = new List<string>();
        private AuthenticationType? _authenticationOverride;
        
        public ServerBase()
        { }

        [XmlAttribute("type")]
        public ServerType Type
        {
            get { return _type; }
            set { _type = value; }
        }

        [XmlElement("hostname")]
        public string Hostname
        {
            get { return _hostname; }
            set { _hostname = value; }
        }

        [XmlElement("port")]
        public int Port
        {
            get { return _port; }
            set { _port = value; }
        }

        [XmlElement("socketType")]
        public SocketType SocketType
        {
            get { return _socketType; }
            set { _socketType = value; }
        }

        [XmlElement("username")]
        public string UsernameFormat
        {
            get { return _usernameFormat; }
            set { _usernameFormat = value; }
        }

        /// <summary>
        /// Authentication methods exactly as the provider lists them. Big providers list several per
        /// server (OAuth2 first), so they are kept as text and interpreted leniently.
        /// </summary>
        [XmlElement("authentication")]
        public List<string> AuthenticationValues
        {
            get { return _authenticationValues; }
            set { _authenticationValues = value ?? new List<string>(); }
        }

        /// <summary>
        /// The method the Wrapper should use: the first password based method the server offers,
        /// otherwise OAuth2 when that is all the server accepts.
        /// </summary>
        [XmlIgnore]
        public AuthenticationType Authentication
        {
            get
            {
                if (_authenticationOverride.HasValue)
                {
                    return _authenticationOverride.Value;
                }

                AuthenticationType best = AuthenticationType.Unknown;
                foreach (string value in _authenticationValues)
                {
                    AuthenticationType parsed = ParseAuthentication(value);
                    if (IsPasswordBased(parsed))
                    {
                        return parsed;
                    }
                    if (parsed != AuthenticationType.Unknown)
                    {
                        best = parsed;
                    }
                }
                return best;
            }
            set { _authenticationOverride = value; }
        }

        /// <summary>True when the server accepts nothing but OAuth2, which the Wrapper cannot do yet.</summary>
        [XmlIgnore]
        public bool IsOAuthOnly
        {
            get { return Authentication == AuthenticationType.OAuth2; }
        }

        private static bool IsPasswordBased(AuthenticationType type)
        {
            switch (type)
            {
                case AuthenticationType.PasswordClearText:
                case AuthenticationType.PasswordEncrypted:
                case AuthenticationType.Plain:
                case AuthenticationType.None:
                case AuthenticationType.ClientIpAddress:
                    return true;
                default:
                    return false;
            }
        }

        private static AuthenticationType ParseAuthentication(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "password-cleartext":
                    return AuthenticationType.PasswordClearText;
                case "password-encrypted":
                case "secure":
                    return AuthenticationType.PasswordEncrypted;
                case "plain":
                    return AuthenticationType.Plain;
                case "none":
                    return AuthenticationType.None;
                case "client-ip-address":
                    return AuthenticationType.ClientIpAddress;
                case "oauth2":
                    return AuthenticationType.OAuth2;
                default:
                    return AuthenticationType.Unknown;
            }
        }

        public string GetUsernameFormatted(string emailAddress)
        {
            string returnVal = null;

            if (!string.IsNullOrEmpty(UsernameFormat) &&
                !string.IsNullOrEmpty(emailAddress))
            {
                string domain = string.Empty;
                string localPart = string.Empty;

                int atIndex = emailAddress.IndexOf(At);

                if (atIndex > 0)
                {
                    domain = emailAddress.Substring(atIndex + 1);
                    localPart = emailAddress.Substring(0, atIndex);
                }

                returnVal = UsernameFormat
                    .Replace(EMAILADDRESS, emailAddress)
                    .Replace(EMAILDOMAIN, domain)
                    .Replace(EMAILLOCALPART, localPart);
            }
            else if (Authentication.Equals(AuthenticationType.None))
            {
                returnVal = string.Empty;
            }
            else
            {
                returnVal = emailAddress;
            }

            return returnVal;
        }
    }
}
