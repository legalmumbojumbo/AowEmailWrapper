using EricDaugherty.CSES.Common;
using EricDaugherty.CSES.SmtpServer;

namespace AowEmailWrapper.CSES
{
    class AnyRecipientFilter : IRecipientFilter
    {
        #region IRecipientFilter Members

        public bool AcceptRecipient(SMTPContext context, EmailAddress recipient)
        {
            return true;
        }

        #endregion
    }
}
