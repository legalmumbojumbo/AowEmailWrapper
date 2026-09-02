using System;
using AowEmailWrapper.ConfigFramework;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace AowEmailWrapper.Helpers
{
    public class ConnectionTestResult
    {
        public bool Success;
        public bool AuthenticationFailed;
        public Exception Error;
        public string Host;
        public string Username;
    }

    /// <summary>
    /// Connects and signs in with the settings the user typed, so problems surface before the next game turn.
    /// </summary>
    public static class ConnectionTester
    {
        private const int TimeoutMs = 20000;

        public static ConnectionTestResult TestIncoming(PollingConfigValues config, string oauthProvider)
        {
            ConnectionTestResult result = new ConnectionTestResult();
            result.Host = config.Server;
            result.Username = config.Username;

            try
            {
                using (IMailService client = config.EmailType == EmailType.IMAP ? (IMailService)new ImapClient() : new Pop3Client())
                {
                    client.Timeout = TimeoutMs;
                    client.Connect(config.Server, config.Port, MailHelper.ToSecureSocketOptions(config.SSLType));
                    MailHelper.Authenticate(client, config.Username, config.PasswordTrue, oauthProvider);
                    client.Disconnect(true);
                }
                result.Success = true;
            }
            catch (AuthenticationException ex)
            {
                result.AuthenticationFailed = true;
                result.Error = ex;
            }
            catch (Exception ex)
            {
                result.Error = ex;
            }

            return result;
        }

        public static ConnectionTestResult TestOutgoing(SmtpConfigValues config, PollingConfigValues pollingConfig, string oauthProvider)
        {
            ConnectionTestResult result = new ConnectionTestResult();
            result.Host = config.SmtpServer;

            string username = null;
            string password = null;

            if (config.Authentication)
            {
                bool usePolling = config.UsePollingCredentials && pollingConfig != null;
                username = usePolling ? pollingConfig.Username : config.Username;
                password = usePolling ? pollingConfig.PasswordTrue : config.PasswordTrue;
            }

            result.Username = username;

            try
            {
                using (SmtpClient client = new SmtpClient())
                {
                    client.Timeout = TimeoutMs;
                    client.Connect(config.SmtpServer, config.Port, MailHelper.ToSecureSocketOptions(config.SmtpSSLType));

                    if (!string.IsNullOrEmpty(username))
                    {
                        MailHelper.Authenticate(client, username, password ?? string.Empty, oauthProvider);
                    }

                    client.Disconnect(true);
                }
                result.Success = true;
            }
            catch (AuthenticationException ex)
            {
                result.AuthenticationFailed = true;
                result.Error = ex;
            }
            catch (Exception ex)
            {
                result.Error = ex;
            }

            return result;
        }
    }
}
