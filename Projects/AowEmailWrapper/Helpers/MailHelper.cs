using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AowEmailWrapper.ConfigFramework;
using MailKit;
using MailKit.Security;
using MimeKit;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// Bridges MimeKit / MailKit objects and the wrapper's own configuration types.
    /// </summary>
    public static class MailHelper
    {
        public static SecureSocketOptions ToSecureSocketOptions(SSLType sslType)
        {
            switch (sslType)
            {
                case SSLType.SSL:
                    return SecureSocketOptions.SslOnConnect;
                case SSLType.TLS:
                    return SecureSocketOptions.StartTls;
                default:
                    //Let MailKit pick: implicit SSL on the well known SSL ports, otherwise STARTTLS when the server offers it.
                    return SecureSocketOptions.Auto;
            }
        }

        /// <summary>
        /// Signs in with a password, or with a Microsoft OAuth token when the account uses Microsoft sign-in.
        /// </summary>
        public static void Authenticate(IMailService client, string username, string password, string oauthProvider)
        {
            if (MicrosoftOAuth.IsProvider(oauthProvider))
            {
                string accessToken = MicrosoftOAuth.AcquireAccessToken(username);
                client.Authenticate(new SaslMechanismOAuth2(username, accessToken));
            }
            else
            {
                client.Authenticate(username, password);
            }
        }

        /// <summary>
        /// Every MIME part that carries a file, whether or not the sender flagged it as an attachment.
        /// </summary>
        public static List<MimePart> GetAttachments(MimeMessage message)
        {
            List<MimePart> attachments = new List<MimePart>();

            if (message != null && message.Body != null)
            {
                foreach (MimeEntity entity in message.BodyParts)
                {
                    MimePart part = entity as MimePart;
                    if (part != null && (part.IsAttachment || !string.IsNullOrEmpty(part.FileName)))
                    {
                        attachments.Add(part);
                    }
                }
            }

            return attachments;
        }

        /// <summary>Header that names the mod (the sender's copy label) a turn was played with.</summary>
        public const string ModHeaderName = "X-AowEmailWrapper-Mod";

        public static string GetModLabel(MimeMessage message)
        {
            string value = message != null ? message.Headers[ModHeaderName] : null;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static void SetModLabel(MimeMessage message, string label)
        {
            if (message == null)
            {
                return;
            }
            message.Headers.RemoveAll(ModHeaderName);
            if (!string.IsNullOrWhiteSpace(label))
            {
                message.Headers.Add(ModHeaderName, label.Trim());
            }
        }

        public static MimePart GetFirstAttachment(MimeMessage message)
        {
            return GetAttachments(message).FirstOrDefault();
        }

        public static byte[] GetAttachmentBytes(MimePart part)
        {
            if (part == null || part.Content == null)
            {
                return new byte[0];
            }

            using (MemoryStream stream = new MemoryStream())
            {
                part.Content.DecodeTo(stream);
                return stream.ToArray();
            }
        }

        public static void SaveAttachment(MimePart part, string path)
        {
            using (FileStream stream = File.Create(path))
            {
                part.Content.DecodeTo(stream);
            }
        }

        public static string GetAttachmentsString(MimeMessage message)
        {
            return string.Join(", ", GetAttachments(message)
                .Select(part => part.FileName)
                .Where(name => !string.IsNullOrEmpty(name)));
        }

        public static string GetPlainText(MimeMessage message)
        {
            return message == null ? string.Empty : (message.TextBody ?? string.Empty);
        }

        /// <summary>
        /// Replaces the plain text body, keeping the attachments and the rest of the MIME tree intact.
        /// </summary>
        public static void SetPlainText(MimeMessage message, string text)
        {
            TextPart textPart = message.BodyParts
                .OfType<TextPart>()
                .FirstOrDefault(part => part.IsPlain && string.IsNullOrEmpty(part.FileName));

            if (textPart != null)
            {
                textPart.SetText(Encoding.UTF8, text);
                return;
            }

            //No plain text part to update, rebuild the body around the attachments
            BodyBuilder builder = new BodyBuilder();
            builder.TextBody = text;
            foreach (MimePart attachment in GetAttachments(message))
            {
                builder.Attachments.Add(attachment);
            }
            message.Body = builder.ToMessageBody();
        }

        public static string GetFromAddress(MimeMessage message)
        {
            MailboxAddress from = (message == null) ? null : message.From.Mailboxes.FirstOrDefault();
            return from == null ? string.Empty : from.Address;
        }

        public static string GetFirstToAddress(MimeMessage message)
        {
            MailboxAddress to = (message == null) ? null : message.To.Mailboxes.FirstOrDefault();
            return to == null ? string.Empty : to.Address;
        }
    }
}
