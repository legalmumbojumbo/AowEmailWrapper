using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AowEmailWrapper.Localization;
using MailKit.Security;
using Microsoft.Identity.Client;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// Microsoft account sign-in (OAuth 2.0) for Outlook.com, Hotmail, Live and Microsoft 365 mailboxes.
    /// The user signs in once in the browser; MSAL keeps the refresh token in a DPAPI protected cache
    /// and access tokens are fetched silently whenever the pollers or the sender need one.
    /// </summary>
    public static class MicrosoftOAuth
    {
        public const string ProviderName = "Microsoft";

        private const string ClientIdKey = "Microsoft.OAuth.ClientId";
        private const string AuthorityKey = "Microsoft.OAuth.Authority";
        //"consumers" matches an app registered for personal Microsoft accounts only; "common" needs the "all account types" registration
        private const string AuthorityDefault = "https://login.microsoftonline.com/consumers";
        private const string RedirectUri = "http://localhost";
        private const string CacheFileName = "microsoft-oauth.cache";

        private const string NotConfiguredKey = "msgOAuthNotConfigured";
        private const string SessionExpiredKey = "msgOAuthSessionExpired";

        private static readonly string[] Scopes =
        {
            "offline_access",
            "https://outlook.office.com/IMAP.AccessAsUser.All",
            "https://outlook.office.com/SMTP.Send"
        };

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AowEmailWrapper.MicrosoftOAuth.v1");
        private static readonly object AppLock = new object();
        private static readonly object CacheLock = new object();
        private static IPublicClientApplication _app;

        public static bool IsProvider(string provider)
        {
            return ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The application (client) ID from the Azure app registration, read from the .config file.</summary>
        public static string ClientId
        {
            get { return (ConfigHelper.GetProperty<string>(ClientIdKey, string.Empty) ?? string.Empty).Trim(); }
        }

        /// <summary>The sign-in endpoint, overridable in the .config file for registrations that allow work accounts too.</summary>
        public static string Authority
        {
            get
            {
                string value = (ConfigHelper.GetProperty<string>(AuthorityKey, string.Empty) ?? string.Empty).Trim();
                return string.IsNullOrEmpty(value) ? AuthorityDefault : value;
            }
        }

        public static bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(ClientId); }
        }

        /// <summary>
        /// Opens the browser for the user to sign in and returns the account name Microsoft reports,
        /// which is the name to use for IMAP and SMTP sign-in from then on.
        /// </summary>
        public static async Task<string> SignInAsync(string loginHint)
        {
            EnsureConfigured();

            AcquireTokenInteractiveParameterBuilder request = GetApp()
                .AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount);

            if (!string.IsNullOrEmpty(loginHint) && loginHint.Contains("@"))
            {
                request = request.WithLoginHint(loginHint.Trim());
            }

            AuthenticationResult result = await request.ExecuteAsync();
            return result.Account.Username;
        }

        /// <summary>
        /// Gets a current access token for the signed in account without user interaction.
        /// Throws <see cref="AuthenticationException"/> when the user has to sign in again, so callers
        /// treat it like any other rejected sign-in.
        /// </summary>
        public static string AcquireAccessToken(string username)
        {
            //Run on the thread pool so this is safe to call from the UI thread as well as the pollers
            return Task.Run(() => AcquireAccessTokenAsync(username)).GetAwaiter().GetResult();
        }

        public static async Task SignOutAsync(string username)
        {
            if (!IsConfigured)
            {
                return;
            }

            IPublicClientApplication app = GetApp();
            foreach (IAccount account in await app.GetAccountsAsync())
            {
                if (string.IsNullOrEmpty(username) || account.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
                {
                    await app.RemoveAsync(account);
                }
            }
        }

        /// <summary>Localised text with an English fallback for contexts where no language table is loaded.</summary>
        private static string Text(string key, string fallback, params string[] args)
        {
            string translated = args.Length == 0 ? Translator.Translate(key) : Translator.Translate(key, args);
            return string.IsNullOrEmpty(translated) ? string.Format(fallback, args) : translated;
        }

        private static string NotConfiguredText()
        {
            return Text(NotConfiguredKey, "Microsoft sign-in is not set up in this copy of the Wrapper. Register the Wrapper in the Azure portal and put the application (client) ID in AowEmailWrapper.dll.config, as described in the README.");
        }

        private static string SessionExpiredText(string username)
        {
            return Text(SessionExpiredKey, "The Microsoft sign-in for {0} has expired or was removed. Open the Accounts tab and sign in with Microsoft again.", username ?? string.Empty);
        }

        private static async Task<string> AcquireAccessTokenAsync(string username)
        {
            if (!IsConfigured)
            {
                throw new AuthenticationException(NotConfiguredText());
            }

            IPublicClientApplication app = GetApp();
            IAccount[] accounts = (await app.GetAccountsAsync()).ToArray();

            IAccount account = accounts.FirstOrDefault(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (account == null && accounts.Length == 1)
            {
                account = accounts[0];
            }

            if (account == null)
            {
                throw new AuthenticationException(SessionExpiredText(username));
            }

            try
            {
                AuthenticationResult result = await app.AcquireTokenSilent(Scopes, account).ExecuteAsync();
                return result.AccessToken;
            }
            catch (MsalUiRequiredException ex)
            {
                throw new AuthenticationException(SessionExpiredText(username), ex);
            }
        }

        private static void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(NotConfiguredText());
            }
        }

        private static IPublicClientApplication GetApp()
        {
            lock (AppLock)
            {
                if (_app == null)
                {
                    _app = PublicClientApplicationBuilder.Create(ClientId)
                        .WithAuthority(Authority)
                        .WithRedirectUri(RedirectUri)
                        .Build();

                    RegisterCache(_app.UserTokenCache);
                }

                return _app;
            }
        }

        #region Token cache (DPAPI protected file in the Wrapper config folder)

        private static string CachePath
        {
            get { return Path.Combine(AppDataHelper.Config.FullName, CacheFileName); }
        }

        private static void RegisterCache(ITokenCache cache)
        {
            cache.SetBeforeAccess(args =>
            {
                lock (CacheLock)
                {
                    if (File.Exists(CachePath))
                    {
                        try
                        {
                            byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(CachePath), Entropy, DataProtectionScope.CurrentUser);
                            args.TokenCache.DeserializeMsalV3(plain);
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceError("Microsoft sign-in cache could not be read, the user will have to sign in again: " + ex.Message);
                            Trace.Flush();
                        }
                    }
                }
            });

            cache.SetAfterAccess(args =>
            {
                if (args.HasStateChanged)
                {
                    lock (CacheLock)
                    {
                        try
                        {
                            byte[] protectedBytes = ProtectedData.Protect(args.TokenCache.SerializeMsalV3(), Entropy, DataProtectionScope.CurrentUser);
                            File.WriteAllBytes(CachePath, protectedBytes);
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceError("Microsoft sign-in cache could not be saved: " + ex.Message);
                            Trace.Flush();
                        }
                    }
                }
            });
        }

        #endregion
    }
}
