using System;
using System.Diagnostics;
using System.Text;
using AowEmailWrapper.ASG;
using AowEmailWrapper.Classes;
using AowEmailWrapper.Helpers;
using EricDaugherty.CSES.SmtpServer;
using MimeKit;
using MimeKit.Utils;

namespace AowEmailWrapper.CSES
{
    public class SmtpSpool : IMessageSpool
    {
        #region Private Members

        private const string EMAIL_APPEND_TEXT = "--------------------------------------------------------\r\nAutosent with the Age of Wonders Email Wrapper [{0}]";

        private MimeMessage _message;

        #endregion

        #region Public Properties

        public MimeMessage SpooledEmail
        {
            get { return _message; }
        }

        #endregion

        #region IMessageSpool Members

        public bool SpoolMessage(MimeMessage message)
        {
            bool isValid = false;

            try
            {
                string originalText = MailHelper.GetPlainText(message);
                string fromAddress = MailHelper.GetFromAddress(message);

                StringBuilder bodyBuilder = new StringBuilder(originalText);

                foreach (MimePart attachment in MailHelper.GetAttachments(message))
                {
                    if (!string.IsNullOrEmpty(attachment.FileName) &&
                        ASGFileInfo.IsAsg(attachment.FileName))
                    {
                        isValid = true;

                        string turnLog = TurnLogger.LogTurn(attachment.FileName, fromAddress, originalText);

                        bodyBuilder.Append(StringHelper.CrLf);
                        bodyBuilder.Append(turnLog);
                    }
                }

                if (isValid)
                {
                    bodyBuilder.Append(StringHelper.CrLf);
                    bodyBuilder.Append(StringHelper.CrLf);
                    bodyBuilder.Append(string.Format(EMAIL_APPEND_TEXT, ConfigHelper.BuildVersion));

                    MailHelper.SetPlainText(message, bodyBuilder.ToString());

                    message.MessageId = MimeUtils.GenerateMessageId();

                    _message = message;
                }
                else
                {
                    _message = null;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                Trace.Flush();
            }

            return isValid;
        }

        #endregion

        #region Constructors

        public SmtpSpool()
        {
        }

        #endregion
    }
}
