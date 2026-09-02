using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Helpers;
using AowEmailWrapper.Localization;

namespace AowEmailWrapper.Controls
{
    public partial class SmtpConfig : UserControl
    {
        private SmtpConfigValues _config;
        public EventHandler Config_Changed;
        public EventHandler TestRequested;

        private System.Windows.Forms.Panel panelTest;
        private string _oauthProvider;
        private System.Windows.Forms.Button buttonTestConnection;

        public SmtpConfig()
        {
            InitializeComponent();

            foreach (SSLType value in Enum.GetValues(typeof(SSLType)))
            {
                fbSSLType.AddItem(value.ToString(), Translator.TranslateEnum(value));
            }

            EventHandler raiseConfigChange = new EventHandler(Raise_Config_Changed);
            fbSmtpServer.InnerTextBox.TextChanged += raiseConfigChange;
            fbPort.InnerTextBox.TextChanged += raiseConfigChange;
            fbEmailAddress.InnerTextBox.TextChanged += raiseConfigChange;
            fbUserName.InnerTextBox.TextChanged += raiseConfigChange;
            fbPassword.InnerTextBox.TextChanged += raiseConfigChange;
            fbSSLType.InnerComboBox.SelectedIndexChanged += raiseConfigChange;

            fbAuthentication.InnerCheckBox.CheckedChanged += raiseConfigChange;            
            fbUsePolling.InnerCheckBox.CheckedChanged += raiseConfigChange;
            fbBccMyself.InnerCheckBox.CheckedChanged += raiseConfigChange;

            fbAuthentication.InnerCheckBox.CheckedChanged += new EventHandler(fbAuthentication_CheckedChanged);
            fbUsePolling.InnerCheckBox.CheckedChanged += new EventHandler(fbUsePolling_CheckedChanged);

            buttonTestConnection.Click += new EventHandler(buttonTestConnection_Click);
        }

        /// <summary>Empty for password sign-in, otherwise the OAuth provider name of the account being edited.</summary>
        public string OAuthProvider
        {
            get { return _oauthProvider; }
            set { _oauthProvider = value; }
        }

        public string Prefix
        {
            get { return "SmtpConfig"; }
        }

        public SmtpConfigValues Config
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
            _config = new SmtpConfigValues();
            _config.Verified = false;

            _config.SmtpServer = fbSmtpServer.TextValue;

            int port = 25;
            if (int.TryParse(fbPort.TextValue, out port))
            {
                _config.Port = port;
            }

            _config.EmailAddress = fbEmailAddress.TextValue;
            _config.Authentication = fbAuthentication.Checked;                       
            _config.UsePollingCredentials = fbUsePolling.Checked;
            _config.BCCMyself = fbBccMyself.Checked;

            _config.SmtpSSLType = ConfigHelper.ParseEnumString<SSLType>(fbSSLType.SelectedValue);

            if (_config.Authentication && !_config.UsePollingCredentials)
            {
                _config.Username = fbUserName.TextValue;
                _config.PasswordTrue = fbPassword.TextValue;                
            }
            else
            {
                _config.Username = string.Empty;
                _config.PasswordTrue = string.Empty;
            }
        }

        private void Populate()
        {
            fbSmtpServer.TextValue = _config.SmtpServer;
            fbPort.TextValue = _config.Port.ToString();
            fbEmailAddress.TextValue = _config.EmailAddress;
            fbSSLType.SelectedValue = _config.SmtpSSLType.ToString();
            fbAuthentication.Checked = _config.Authentication;            
            fbUsePolling.Checked = _config.UsePollingCredentials;
            fbBccMyself.Checked = _config.BCCMyself;

            if (_config.Authentication && !_config.UsePollingCredentials)
            {
                fbUserName.TextValue = _config.Username;
                fbPassword.TextValue = _config.PasswordTrue;
            }
            else
            {
                fbUserName.TextValue = string.Empty;
                fbPassword.TextValue = string.Empty;
            }

            if (MicrosoftOAuth.IsProvider(_oauthProvider))
            {
                //Sending signs in with the same Microsoft token as polling; there is nothing to type here
                fbAuthentication.Checked = true;
                fbUsePolling.Checked = true;
            }

            groupBoxAuth.Visible = _config.Authentication && !MicrosoftOAuth.IsProvider(_oauthProvider);
            fbUserName.Enabled = !fbUsePolling.Checked;
            fbPassword.Enabled = !fbUsePolling.Checked;
        }

        private void fbAuthentication_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxAuth.Visible = fbAuthentication.Checked && !MicrosoftOAuth.IsProvider(_oauthProvider);
        }

        private void fbUsePolling_CheckedChanged(object sender, EventArgs e)
        {
            fbUserName.Enabled = !fbUsePolling.Checked;
            fbPassword.Enabled = !fbUsePolling.Checked;
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
