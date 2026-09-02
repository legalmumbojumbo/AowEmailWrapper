using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using AowEmailWrapper.Games;
using MimeKit;

namespace AowEmailWrapper.CSES
{
    public class SmtpSendResponse: IDisposable
    {
        #region Private Members

        private MimeMessage _theGameEmail;
        private bool _isSuccess;
        private Exception _ex;

        #endregion

        #region Public Properties

        public MimeMessage GameEmail
        {
            get { return _theGameEmail; }
            set { _theGameEmail = value; }
        }

        public bool IsSuccess
        {
            get { return _isSuccess; }
            set { _isSuccess = value; }
        }

        public Exception Exception
        {
            get { return _ex; }
            set { _ex = value; }
        }

        #endregion

        #region Constructors

        public SmtpSendResponse(MimeMessage theGameEmail, bool isSuccess)
            : this(theGameEmail, isSuccess, null)
        { }

        public SmtpSendResponse(MimeMessage theGameEmail, bool isSuccess, Exception ex)
        {
            _theGameEmail = theGameEmail;
            _isSuccess = isSuccess;
            _ex = ex;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (_theGameEmail != null)
            {
                _theGameEmail = null;
            }
            _ex = null;
        }

        #endregion
    }
}
