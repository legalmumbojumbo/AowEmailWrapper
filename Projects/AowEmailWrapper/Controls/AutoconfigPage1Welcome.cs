using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AowEmailWrapper.Helpers;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Localization;
using AowEmailWrapper.Classes;
using Mozilla.Autoconfig;

namespace AowEmailWrapper.Controls
{
    public partial class AutoconfigPage1Welcome : UserControl
    {
        public enum AutoconfigPage1Outcome
        {
            Unknown,
            Success
        }

        private const string ServerPreferenceNoPreferenceKey = "serverPreferenceNoPreference";

        public KeyEventHandler TextKeyDown;
        public EventHandler Next;
        /// <summary>Raised whenever the address or password text changes, including pastes.</summary>
        public EventHandler InputChanged;

        private const string LinkCreateAppPasswordKey = "linkCreateAppPassword";
        private const string OAuthSignedInKey = "msgOAuthSignedIn";
        private const string OAuthTitleKey = "buttonSignInMicrosoft";
        private const string ButtonOKKey = "buttonOK";
        private System.Windows.Forms.LinkLabel linkPasswordHint;
        private System.Windows.Forms.Button buttonSignInMicrosoft;
        private ProviderHint _hint;
        private string _oauthProvider;
        private string _oauthUsername;

        public AutoconfigPage1Welcome()
        {
            InitializeComponent();

            KeyEventHandler textBoxKeyDown = new KeyEventHandler(textBox_KeyDown);

            fbEmailAddress.InnerTextBox.KeyDown += textBoxKeyDown;
            fbPassword.InnerTextBox.KeyDown += textBoxKeyDown;

            fbEmailAddress.InnerTextBox.TextChanged += new EventHandler(emailAddress_TextChanged);
            fbEmailAddress.InnerTextBox.TextChanged += new EventHandler(input_TextChanged);
            fbPassword.InnerTextBox.TextChanged += new EventHandler(input_TextChanged);
            linkPasswordHint.LinkClicked += new LinkLabelLinkClickedEventHandler(linkPasswordHint_LinkClicked);
            buttonSignInMicrosoft.Click += new EventHandler(buttonSignInMicrosoft_Click);
        }

        public AutoconfigPage1Outcome Outcome
        {
            get
            {
                AutoconfigPage1Outcome returnVal = AutoconfigPage1Outcome.Unknown;
                if (fbEmailAddress.TextValue.Length > 0 &&
                    (fbPassword.TextValue.Length > 0 || !string.IsNullOrEmpty(_oauthProvider)))
                {
                    returnVal = AutoconfigPage1Outcome.Success;
                }
                return returnVal;
            }
        }

        private void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (TextKeyDown != null)
            {
                TextKeyDown(sender, e);
            }

            if (e.KeyCode.Equals(Keys.Enter) && 
                sender.Equals(fbPassword.InnerTextBox) &&
                Next !=null)
            {
                Next(this, e);
            }
        }

        public string EmailAddress
        {
            get { return fbEmailAddress.TextValue; }
        }

        /// <summary>Empty for password sign-in, otherwise the OAuth provider the user signed in with.</summary>
        public string OAuthProvider
        {
            get { return _oauthProvider; }
        }

        /// <summary>The account name reported by the OAuth provider, which is the IMAP and SMTP sign-in name.</summary>
        public string OAuthUsername
        {
            get { return _oauthUsername; }
        }

        public string Password
        {
            get { return fbPassword.TextValue; }
        }

        private void input_TextChanged(object sender, EventArgs e)
        {
            if (InputChanged != null)
            {
                InputChanged(this, e);
            }
        }

        private void emailAddress_TextChanged(object sender, EventArgs e)
        {
            UpdateHint();
        }

        /// <summary>
        /// Providers such as Gmail need an app password rather than the account password;
        /// say so as soon as the address shows which provider this is.
        /// </summary>
        private void UpdateHint()
        {
            _hint = ProviderHints.ForEmailAddress(fbEmailAddress.TextValue);
            bool microsoft = _hint != null && _hint.MessageKey == ProviderHints.MicrosoftMessageKey;

            //Microsoft accounts sign in through the browser instead of a password
            fbPassword.Visible = !microsoft;
            buttonSignInMicrosoft.Visible = microsoft;
            if (!microsoft)
            {
                _oauthProvider = null;
                _oauthUsername = null;
            }

            if (_hint == null)
            {
                linkPasswordHint.Text = string.Empty;
                linkPasswordHint.Visible = false;
                return;
            }

            string message = !string.IsNullOrEmpty(_oauthProvider)
                ? Translator.Translate(OAuthSignedInKey, _oauthUsername)
                : Translator.Translate(_hint.MessageKey);
            string linkText = string.IsNullOrEmpty(_hint.Url) ? string.Empty : Translator.Translate(LinkCreateAppPasswordKey);

            if (string.IsNullOrEmpty(linkText))
            {
                linkPasswordHint.Text = message;
                linkPasswordHint.LinkArea = new LinkArea(0, 0);
            }
            else
            {
                linkPasswordHint.Text = message + " " + linkText;
                linkPasswordHint.LinkArea = new LinkArea(message.Length + 1, linkText.Length);
            }

            linkPasswordHint.Visible = true;
        }

        private async void buttonSignInMicrosoft_Click(object sender, EventArgs e)
        {
            buttonSignInMicrosoft.Enabled = false;
            try
            {
                _oauthUsername = await MicrosoftOAuth.SignInAsync(fbEmailAddress.TextValue);
                _oauthProvider = MicrosoftOAuth.ProviderName;
                UpdateHint();
                if (InputChanged != null)
                {
                    InputChanged(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                ExceptionDialog.Show(this, Translator.Translate(OAuthTitleKey), ex, MessageBoxIcon.Error, Translator.Translate(ButtonOKKey));
            }
            finally
            {
                buttonSignInMicrosoft.Enabled = true;
            }
        }

        private void linkPasswordHint_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_hint != null && !string.IsNullOrEmpty(_hint.Url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_hint.Url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(ex.ToString());
                }
            }
        }

        public void Reset()
        {
            fbEmailAddress.InnerTextBox.Focus();
        }
    }
}
