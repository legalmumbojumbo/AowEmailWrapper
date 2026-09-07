using System;
using System.Diagnostics;
using AowEmailWrapper.ConfigFramework;
using MailKit.Net.Smtp;
using MimeKit;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// Sends a message the Wrapper itself composed (not a game turn) through an account's SMTP
    /// server. Synchronous; callers run it off the UI thread.
    /// </summary>
    public static class WrapperMailer
    {
        private const int TimeoutMs = 60000;

        /// <summary>The address an account sends as, or null when the account cannot send.</summary>
        public static string SenderAddress(AccountConfigValues account)
        {
            return BugReportHelper.SenderAddress(account);
        }

        public static bool CanSend(AccountConfigValues account)
        {
            return account != null && account.SmtpConfig != null && !string.IsNullOrEmpty(account.SmtpConfig.SmtpServer) && SenderAddress(account) != null;
        }

        public static void Send(AccountConfigValues account, MimeMessage message)
        {
            if (!CanSend(account))
            {
                throw new InvalidOperationException("The account has no outgoing mail server.");
            }

            SmtpConfigValues smtp = account.SmtpConfig;
            PollingConfigValues polling = account.PollingConfig;

            string username = null;
            string password = null;
            if (smtp.Authentication)
            {
                bool usePolling = smtp.UsePollingCredentials && polling != null;
                username = usePolling ? polling.Username : smtp.Username;
                password = usePolling ? polling.PasswordTrue : smtp.PasswordTrue;
            }

            using (SmtpClient client = new SmtpClient())
            {
                client.Timeout = TimeoutMs;
                client.Connect(smtp.SmtpServer, smtp.Port, MailHelper.ToSecureSocketOptions(smtp.SmtpSSLType));

                if (!string.IsNullOrEmpty(username))
                {
                    MailHelper.Authenticate(client, username, password ?? string.Empty, account.OAuthProvider);
                }

                client.Send(message);
                client.Disconnect(true);
            }

            Trace.TraceInformation("Wrapper message '{0}' sent to {1} from {2}", message.Subject, MailHelper.GetFirstToAddress(message), SenderAddress(account));
        }
    }
}
