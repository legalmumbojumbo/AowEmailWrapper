using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AowEmailWrapper.Controls
{
    /// <summary>
    /// Replacement for the retired Microsoft.ExceptionMessageBox: shows the exception chain
    /// with an expandable details pane and caller supplied buttons.
    /// </summary>
    public class ExceptionDialog : Form
    {
        private const int Pad = 16;
        private const int ButtonHeight = 26;
        private const int DetailsHeight = 230;

        private int _collapsedHeight;
        private int _expandedHeight;

        private int _result = -1;
        private TextBox _details;
        private Button _detailsButton;

        /// <summary>
        /// Shows the dialog and returns the zero based index of the button that was clicked,
        /// or -1 if the dialog was closed some other way.
        /// </summary>
        public static int Show(IWin32Window owner, string caption, Exception ex, MessageBoxIcon icon, params string[] buttons)
        {
            using (ExceptionDialog dialog = new ExceptionDialog(caption, ex, icon, buttons))
            {
                dialog.ShowDialog(owner);
                return dialog._result;
            }
        }

        private ExceptionDialog(string caption, Exception ex, MessageBoxIcon icon, string[] buttons)
        {
            Text = caption ?? string.Empty;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 200);

            PictureBox iconBox = new PictureBox();
            iconBox.Image = GetIcon(icon);
            iconBox.SizeMode = PictureBoxSizeMode.AutoSize;
            iconBox.Location = new Point(16, 16);
            Controls.Add(iconBox);

            //Size the message area to its text so long provider advice is never cut off
            string messageText = BuildMessage(ex);
            int messageWidth = ClientSize.Width - 80;
            int textHeight = TextRenderer.MeasureText(messageText, Font, new Size(messageWidth, int.MaxValue), TextFormatFlags.WordBreak).Height;
            int labelHeight = Math.Min(400, Math.Max(48, textHeight + 8));
            _collapsedHeight = Pad + labelHeight + Pad + ButtonHeight + Pad;
            _expandedHeight = _collapsedHeight + DetailsHeight;
            ClientSize = new Size(ClientSize.Width, _collapsedHeight);

            Label message = new Label();
            message.Text = messageText;
            message.Location = new Point(64, Pad);
            message.Size = new Size(messageWidth, labelHeight);
            Controls.Add(message);

            _details = new TextBox();
            _details.Multiline = true;
            _details.ReadOnly = true;
            _details.ScrollBars = ScrollBars.Both;
            _details.WordWrap = false;
            _details.Font = new Font(FontFamily.GenericMonospace, 8.25f);
            _details.Text = ex == null ? string.Empty : ex.ToString();
            _details.Location = new Point(Pad, _collapsedHeight);
            _details.Size = new Size(ClientSize.Width - 2 * Pad, DetailsHeight - Pad);
            _details.Visible = false;
            Controls.Add(_details);

            _detailsButton = new Button();
            _detailsButton.Text = "Details >>";
            _detailsButton.Size = new Size(90, ButtonHeight);
            _detailsButton.Location = new Point(Pad, _collapsedHeight - Pad - ButtonHeight);
            _detailsButton.Click += new EventHandler(DetailsButton_Click);
            Controls.Add(_detailsButton);

            int x = ClientSize.Width - 16;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                Button button = new Button();
                button.Text = buttons[i];
                button.Tag = i;
                button.Size = new Size(Math.Max(90, TextRenderer.MeasureText(buttons[i], Font).Width + 24), ButtonHeight);
                x -= button.Size.Width;
                button.Location = new Point(x, _collapsedHeight - Pad - ButtonHeight);
                x -= 8;
                button.Click += new EventHandler(Button_Click);
                Controls.Add(button);

                if (i == 0)
                {
                    AcceptButton = button;
                }
                if (i == buttons.Length - 1)
                {
                    CancelButton = button;
                }
            }
        }

        private void Button_Click(object sender, EventArgs e)
        {
            _result = (int)((Button)sender).Tag;
            Close();
        }

        private void DetailsButton_Click(object sender, EventArgs e)
        {
            bool expand = !_details.Visible;
            _details.Visible = expand;
            _detailsButton.Text = expand ? "<< Details" : "Details >>";
            ClientSize = new Size(ClientSize.Width, expand ? _expandedHeight : _collapsedHeight);
        }

        private static string BuildMessage(Exception ex)
        {
            List<string> lines = new List<string>();
            for (Exception current = ex; current != null; current = current.InnerException)
            {
                if (!string.IsNullOrEmpty(current.Message) && !lines.Contains(current.Message))
                {
                    lines.Add(current.Message);
                }
            }
            return string.Join(Environment.NewLine + Environment.NewLine, lines);
        }

        private static Image GetIcon(MessageBoxIcon icon)
        {
            switch (icon)
            {
                case MessageBoxIcon.Question:
                    return SystemIcons.Question.ToBitmap();
                case MessageBoxIcon.Warning:
                    return SystemIcons.Warning.ToBitmap();
                case MessageBoxIcon.Information:
                    return SystemIcons.Information.ToBitmap();
                default:
                    return SystemIcons.Error.ToBitmap();
            }
        }
    }
}
