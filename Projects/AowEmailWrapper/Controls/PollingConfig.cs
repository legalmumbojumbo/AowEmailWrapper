using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Helpers;
using AowEmailWrapper.Localization;

namespace AowEmailWrapper.Controls
{
    public partial class PollingConfig : UserControl
    {
        public EventHandler Config_Changed;
        public EventHandler TestRequested;

        private System.Windows.Forms.Panel panelTest;
        private System.Windows.Forms.LinkLabel linkOAuthAccount;
        private string _oauthProvider;

        private const string OAuthAccountKey = "lblOAuthAccount";
        private const string OAuthSignInAgainKey = "linkOAuthSignInAgain";
        private const string OAuthSignedInKey = "msgOAuthSignedIn";
        private const string OAuthTitleKey = "buttonSignInMicrosoft";
        private const string ButtonOKKey = "buttonOK";
        private System.Windows.Forms.Button buttonTestConnection;

        private PollingConfigValues _config;

        public PollingConfig()
        {
            InitializeComponent();
            foreach (EmailType value in Enum.GetValues(typeof(EmailType)))
            {
                fbEmailType.AddItem(value.ToString(), Translator.TranslateEnum(value));
            }
            
            fbEmailType.SelectedIndex = 0;

            foreach (int i in new int[] { 1, 2, 3, 4, 5, 10, 15, 30, 60 })
            {
                fbPollingSetup.AddItem(i);
            }

            fbPollingSetup.SelectedIndex = 0;

            foreach (SSLType value in Enum.GetValues(typeof(SSLType)))
            {
                fbSSLType.AddItem(value.ToString(), Translator.TranslateEnum(value));
            }

            fbSSLType.SelectedIndex = 0;

            EventHandler raiseConfigChange = new EventHandler(Raise_Config_Changed);

            fbEmailType.InnerComboBox.SelectedIndexChanged += raiseConfigChange;
            fbServer.InnerTextBox.TextChanged += raiseConfigChange;
            fbPort.InnerTextBox.TextChanged += raiseConfigChange;
            fbUserName.InnerTextBox.TextChanged += raiseConfigChange;
            fbPassword.InnerTextBox.TextChanged += raiseConfigChange;
            fbPollingSetup.InnerComboBox.SelectedIndexChanged += raiseConfigChange;

            fbPollingSetup.InnerCheckBox.CheckedChanged += raiseConfigChange;
            fbPollingSetup.InnerCheckBox.CheckedChanged += new EventHandler(fbPollingSetup_CheckedChanged);
            fbSSLType.InnerComboBox.SelectedIndexChanged += raiseConfigChange;

            buttonTestConnection.Click += new EventHandler(buttonTestConnection_Click);
            linkOAuthAccount.LinkClicked += new LinkLabelLinkClickedEventHandler(linkOAuthAccount_LinkClicked);
        }

        /// <summary>Empty for password sign-in, otherwise the OAuth provider name of the account being edited.</summary>
        public string OAuthProvider
        {
            get { return _oauthProvider; }
            set { _oauthProvider = value; ApplyOAuthLayout(); }
        }

        /// <summary>The Check for email box, which is what makes an account active.</summary>
        public bool ChecksForEmail
        {
            get { return fbPollingSetup.Checked; }
            set { fbPollingSetup.Checked = value; }
        }

        public string Prefix
        {
            get { return "PollingConfig"; }
        }

        public PollingConfigValues Config
        {
            get
            {
                Scrape();
                return _config;
            }
            set
            {
                _config = value;
                Populate();
            }
        }

        private void Scrape()
        {
            _config = new PollingConfigValues();

            _config.UsePolling = fbPollingSetup.Checked;

            _config.EmailType = ConfigHelper.ParseEnumString<EmailType>(fbEmailType.SelectedValue);

            _config.Server = fbServer.TextValue;

            int port = 110;
            if (int.TryParse(fbPort.TextValue, out port))
            {
                _config.Port = port;
            }

            _config.SSLType = ConfigHelper.ParseEnumString<SSLType>(fbSSLType.SelectedValue);
            _config.Username = fbUserName.TextValue;
            _config.PasswordTrue = fbPassword.TextValue;

            int poll = 10;
            if (int.TryParse(fbPollingSetup.SelectedValue, out poll))
            {
                _config.PollInterval = poll;
            }
        }

        private void Populate()
        {
            fbPollingSetup.Checked = _config.UsePolling;
            fbEmailType.SelectedValue = _config.EmailType.ToString();
            fbServer.TextValue = _config.Server;
            fbPort.TextValue = _config.Port.ToString();
            fbSSLType.SelectedValue = _config.SSLType.ToString();
            fbUserName.TextValue = _config.Username;
            fbPassword.TextValue = _config.PasswordTrue;
            fbPollingSetup.SelectedValue = _config.PollInterval.ToString();
            panelTest.Visible = _config.UsePolling;
            ApplyOAuthLayout();
        }

        private void fbPollingSetup_CheckedChanged(object sender, EventArgs e)
        {
            this.SuspendLayout();

            groupBoxAuth.Visible = fbPollingSetup.Checked;
            groupBoxServer.Visible = fbPollingSetup.Checked;
            panelTest.Visible = fbPollingSetup.Checked;

            this.ResumeLayout();
        }

        /// <summary>An OAuth account has no password; show how it signs in instead of the password box.</summary>
        private void ApplyOAuthLayout()
        {
            bool oauth = MicrosoftOAuth.IsProvider(_oauthProvider);

            fbPassword.Visible = !oauth;
            linkOAuthAccount.Visible = oauth;

            if (oauth)
            {
                string message = Translator.Translate(OAuthAccountKey);
                string linkText = Translator.Translate(OAuthSignInAgainKey);
                linkOAuthAccount.Text = message + " " + linkText;
                linkOAuthAccount.LinkArea = new LinkArea(message.Length + 1, linkText.Length);
            }
        }

        private async void linkOAuthAccount_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                string user = await MicrosoftOAuth.SignInAsync(fbUserName.TextValue);
                fbUserName.TextValue = user;
                MessageBox.Show(this, Translator.Translate(OAuthSignedInKey, user), Translator.Translate(OAuthTitleKey), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ExceptionDialog.Show(this, Translator.Translate(OAuthTitleKey), ex, MessageBoxIcon.Error, Translator.Translate(ButtonOKKey));
            }
        }

        private void buttonTestConnection_Click(object sender, EventArgs e)
        {
            if (TestRequested != null)
            {
                TestRequested(this, e);
            }
        }

        private void Raise_Config_Changed(object sender, EventArgs e)
        {
            if (Config_Changed != null)
            {
                Config_Changed(this, e);
            }
        }
    }
}
