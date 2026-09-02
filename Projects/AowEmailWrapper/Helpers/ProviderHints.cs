using System;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// Sign-in advice for a mail provider: a localisation key for the message and an optional page to open.
    /// </summary>
    public class ProviderHint
    {
        public string MessageKey;
        /// <summary>One sentence version for tray balloons, which are limited to about 250 characters.</summary>
        public string ShortMessageKey;
        public string Url;

        public ProviderHint(string messageKey, string url)
        {
            MessageKey = messageKey;
            ShortMessageKey = messageKey + "Short";
            Url = url;
        }
    }

    /// <summary>
    /// The big providers no longer accept a normal account password from mail programs.
    /// These hints tell the user what to enter instead, both in the account wizard and when sign-in fails.
    /// </summary>
    public static class ProviderHints
    {
        public const string GmailMessageKey = "msgHintGmail";
        public const string YahooMessageKey = "msgHintYahoo";
        public const string MicrosoftMessageKey = "msgHintMicrosoft";

        private const string GmailAppPasswordUrl = "https://myaccount.google.com/apppasswords";
        private const string YahooAppPasswordUrl = "https://login.yahoo.com/account/security";

        private static readonly string[] GmailDomains = { "gmail.com", "googlemail.com" };
        private static readonly string[] YahooDomains = { "yahoo.", "ymail.com", "rocketmail.com", "aol.com" };
        private static readonly string[] MicrosoftDomains = { "outlook.", "hotmail.", "live.", "msn.com" };

        private static readonly string[] GmailHosts = { "gmail.com", "googlemail.com", "google.com" };
        private static readonly string[] YahooHosts = { "yahoo.", "aol.com" };
        private static readonly string[] MicrosoftHosts = { "outlook.", "office365.com", "hotmail.", "live.com" };

        /// <summary>Hint for the account that owns this email address, or null when none applies.</summary>
        public static ProviderHint ForEmailAddress(string emailAddress)
        {
            if (string.IsNullOrEmpty(emailAddress))
            {
                return null;
            }

            int atIndex = emailAddress.IndexOf('@');
            if (atIndex < 0 || atIndex == emailAddress.Length - 1)
            {
                return null;
            }

            return ForName(emailAddress.Substring(atIndex + 1).Trim().ToLowerInvariant(), GmailDomains, YahooDomains, MicrosoftDomains);
        }

        /// <summary>Hint for the mail server host name, or null when none applies.</summary>
        public static ProviderHint ForHost(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return null;
            }

            return ForName(host.Trim().ToLowerInvariant(), GmailHosts, YahooHosts, MicrosoftHosts);
        }

        private static ProviderHint ForName(string name, string[] gmail, string[] yahoo, string[] microsoft)
        {
            if (Matches(name, gmail))
            {
                return new ProviderHint(GmailMessageKey, GmailAppPasswordUrl);
            }
            if (Matches(name, yahoo))
            {
                return new ProviderHint(YahooMessageKey, YahooAppPasswordUrl);
            }
            if (Matches(name, microsoft))
            {
                return new ProviderHint(MicrosoftMessageKey, null);
            }
            return null;
        }

        private static bool Matches(string name, string[] patterns)
        {
            foreach (string pattern in patterns)
            {
                //Patterns ending in a dot match any top level domain (yahoo.com, yahoo.co.uk, ...)
                if (pattern.EndsWith(".") ? name.Contains(pattern) : (name == pattern || name.EndsWith("." + pattern)))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
