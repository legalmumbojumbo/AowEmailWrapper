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
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Theme.Apply(this);
        }

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
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            //The layout is written in 96 dpi units while the text is drawn at the screen's dpi, so every
            //box is scaled to the screen and every text height is measured rather than assumed
            float dpi = DeviceDpi / 96f;
            Func<int, int> scaled = value => (int)Math.Round(value * dpi);
            int pad = scaled(Pad);
            int width = scaled(DialogWidth);
            int textWidth = width - pad * 2;
            //The theme swaps the text font in OnLoad, so measure with the font the text will actually use
            Font measureFont = Theme.Enabled ? Theme.BodyFont : Font;

            int y = pad;

            Label intro = new Label();
            intro.Location = new Point(pad, y);
            intro.Text = account != null
                ? Translator.Translate(IntroKey, BugReportHelper.SenderAddress(account))
                : Translator.Translate(IntroNoAccountKey);
            int introHeight = TextRenderer.MeasureText(intro.Text, measureFont, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            intro.Size = new Size(textWidth, introHeight + scaled(4));
            intro.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(intro);
            y = intro.Bottom + scaled(4);

            _description = new TextBox();
            _description.Multiline = true;
            _description.AcceptsReturn = true;
            _description.ScrollBars = ScrollBars.Vertical;
            _description.Location = new Point(pad, y);
            _description.Size = new Size(textWidth, scaled(170));
            _description.MaxLength = 20000;
            //The description is the part that grows when the dialog is made larger
            _description.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_description);
            y = _description.Bottom + scaled(10);

            //A fixed width so long translations wrap instead of running off the dialog
            _attachLog = new CheckBox();
            _attachLog.Checked = true;
            _attachLog.Location = new Point(pad, y);
            _attachLog.Text = Translator.Translate(AttachLogKey);
            _attachLog.Visible = account != null;
            _attachLog.Width = textWidth;
            //ButtonBase's preferred size ignores wrapping, so measure the wrapped text beside the check glyph
            Size textArea = new Size(_attachLog.Width - scaled(CheckGlyphWidth), int.MaxValue);
            _attachLog.Height = TextRenderer.MeasureText(_attachLog.Text, measureFont, textArea, TextFormatFlags.WordBreak).Height + scaled(6);
            _attachLog.TextAlign = ContentAlignment.TopLeft;
            _attachLog.CheckAlign = ContentAlignment.TopLeft;
            _attachLog.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_attachLog);
            if (_attachLog.Visible)
            {
                y = _attachLog.Bottom + scaled(10);
            }

            int buttonWidth = scaled(84);
            int buttonHeight = scaled(26);

            _status = new Label();
            _status.AutoEllipsis = true;
            _status.Location = new Point(pad, y + scaled(5));
            _status.Size = new Size(textWidth - buttonWidth * 2 - scaled(24), scaled(20));
            _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_status);

            _send = new Button();
            _send.Text = Translator.Translate(SendKey);
            _send.Size = new Size(buttonWidth, buttonHeight);
            _send.Location = new Point(width - pad - buttonWidth * 2 - scaled(8), y);
            _send.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _send.Click += Send_Click;
            Controls.Add(_send);

            _cancel = new Button();
            _cancel.Text = Translator.Translate(CancelKey);
            _cancel.Size = _send.Size;
            _cancel.Location = new Point(width - pad - buttonWidth, y);
            _cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(_cancel);

            //No AcceptButton: Enter adds a line to the description
            CancelButton = _cancel;
            ClientSize = new Size(width, y + buttonHeight + pad);
            //It can be made larger for a long description, never smaller than the text needs
            MinimumSize = Size;

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
