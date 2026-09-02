using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Helpers;
using AowEmailWrapper.Localization;

namespace AowEmailWrapper.Controls
{
    /// <summary>
    /// Lets the player describe a problem and emails the description, with the Wrapper's log file
    /// attached, to the maintainer. When no account can send email yet the report is handed to the
    /// player's own email program instead.
    /// </summary>
    public class BugReportForm : Form
    {
        private const string TitleKey = "msgBugReportTitle";
        private const string IntroKey = "msgBugReportIntro";
        private const string IntroNoAccountKey = "msgBugReportIntroNoAccount";
        private const string AttachLogKey = "chkBugReportAttachLog";
        private const string SendKey = "buttonSend";
        private const string CancelKey = "buttonCancel";
        private const string EmptyKey = "msgBugReportEmpty";
        private const string SendingKey = "msgBugReportSending";
        private const string SentKey = "msgBugReportSent";
        private const string FailedKey = "msgBugReportFailed";
        private const int Pad = 16;
        private const int DialogWidth = 480;
        private const int CheckGlyphWidth = 24;

        private readonly AccountConfigValues _account;
        private readonly TextBox _description;
        private readonly CheckBox _attachLog;
        private readonly Label _status;
        private readonly Button _send;
        private readonly Button _cancel;
        private bool _sending;

        /// <summary>Shows the dialog. The account may be null, in which case the email program fallback is used.</summary>
        public static void Show(IWin32Window owner, AccountConfigValues account)
        {
            using (BugReportForm form = new BugReportForm(account))
            {
                form.ShowDialog(owner);
            }
        }

        private BugReportForm(AccountConfigValues account)
        {
            _account = account;

            Text = Translator.Translate(TitleKey);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            int y = Pad;

            Label intro = new Label();
            intro.Location = new Point(Pad, y);
            intro.Size = new Size(DialogWidth - Pad * 2, 64);
            intro.Text = account != null
                ? Translator.Translate(IntroKey, BugReportHelper.SenderAddress(account))
                : Translator.Translate(IntroNoAccountKey);
            Controls.Add(intro);
            y = intro.Bottom + 4;

            _description = new TextBox();
            _description.Multiline = true;
            _description.AcceptsReturn = true;
            _description.ScrollBars = ScrollBars.Vertical;
            _description.Location = new Point(Pad, y);
            _description.Size = new Size(DialogWidth - Pad * 2, 170);
            _description.MaxLength = 20000;
            Controls.Add(_description);
            y = _description.Bottom + 10;

            //A fixed width so long translations wrap instead of running off the dialog
            _attachLog = new CheckBox();
            _attachLog.Checked = true;
            _attachLog.Location = new Point(Pad, y);
            _attachLog.Text = Translator.Translate(AttachLogKey);
            _attachLog.Visible = account != null;
            _attachLog.Width = DialogWidth - Pad * 2;
            //ButtonBase's preferred size ignores wrapping, so measure the wrapped text beside the check glyph
            Size textArea = new Size(_attachLog.Width - CheckGlyphWidth, int.MaxValue);
            _attachLog.Height = TextRenderer.MeasureText(_attachLog.Text, _attachLog.Font, textArea, TextFormatFlags.WordBreak).Height + 6;
            _attachLog.TextAlign = ContentAlignment.TopLeft;
            _attachLog.CheckAlign = ContentAlignment.TopLeft;
            Controls.Add(_attachLog);
            if (_attachLog.Visible)
            {
                y = _attachLog.Bottom + 10;
            }

            _status = new Label();
            _status.AutoEllipsis = true;
            _status.Location = new Point(Pad, y + 5);
            _status.Size = new Size(DialogWidth - Pad * 2 - 200, 20);
            Controls.Add(_status);

            _send = new Button();
            _send.Text = Translator.Translate(SendKey);
            _send.Size = new Size(84, 26);
            _send.Location = new Point(DialogWidth - Pad - _send.Width * 2 - 8, y);
            _send.Click += Send_Click;
            Controls.Add(_send);

            _cancel = new Button();
            _cancel.Text = Translator.Translate(CancelKey);
            _cancel.Size = _send.Size;
            _cancel.Location = new Point(DialogWidth - Pad - _cancel.Width, y);
            _cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(_cancel);

            //No AcceptButton: Enter adds a line to the description
            CancelButton = _cancel;
            ClientSize = new Size(DialogWidth, y + _send.Height + Pad);

            FormClosing += BugReportForm_FormClosing;
        }

        private async void Send_Click(object sender, EventArgs e)
        {
            string description = _description.Text.Trim();
            if (description.Length == 0)
            {
                MessageBox.Show(this, Translator.Translate(EmptyKey), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                _description.Focus();
                return;
            }

            if (_account == null)
            {
                try
                {
                    BugReportHelper.OpenMailClient(description);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Bug report could not be handed to the email program: {0}", ex);
                    MessageBox.Show(this, Translator.Translate(FailedKey, ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            SetSending(true);
            try
            {
                await BugReportHelper.SendAsync(_account, description, _attachLog.Checked);
                SetSending(false);
                MessageBox.Show(this, Translator.Translate(SentKey, BugReportHelper.Email), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                //Keep the dialog open so the description is not lost
                Trace.TraceError("Bug report could not be sent: {0}", ex);
                SetSending(false);
                MessageBox.Show(this, Translator.Translate(FailedKey, ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetSending(bool sending)
        {
            _sending = sending;
            _status.Text = sending ? Translator.Translate(SendingKey) : string.Empty;
            _description.ReadOnly = sending;
            _attachLog.Enabled = !sending;
            _send.Enabled = !sending;
            _cancel.Enabled = !sending;
            UseWaitCursor = sending;
        }

        private void BugReportForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_sending)
            {
                e.Cancel = true;
            }
        }
    }
}
