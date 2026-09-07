using System;
using System.Windows.Forms;
using AowEmailWrapper.Pollers.MessageStore;
using AowEmailWrapper.Helpers;
using AowEmailWrapper.Localization;

namespace AowEmailWrapper
{
    public partial class MessageStore : Form
    {
        private string _username;
        private string _host;

        public MessageStore(string username, string host)
        {
            InitializeComponent();
            Theme.Apply(this);

            Translator.TranslateForm(this);

            _username = username;
            _host = host;

            messageStoreList.Messages = MessageStoreManager.LoadLocalMessageStore(_username, _host);
            this.Text = Translator.Translate(this.Name, _username, _host);

            this.KeyPreview = true;
            this.KeyPress += new KeyPressEventHandler(MessageStore_KeyPress);            
        }

        private void MessageStore_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar.Equals((char)Keys.Escape))
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private void cmdRedownload_Click(object sender, EventArgs e)
        {
            MessageStoreCollection theMessageStore = messageStoreList.Messages;

            if (messageStoreList.ItemsRemoved)
            {
                MessageStoreManager.SaveLocalMessageStore(_username, _host, theMessageStore);
                this.DialogResult = DialogResult.OK;
            }

            this.Close();
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

    }
}
