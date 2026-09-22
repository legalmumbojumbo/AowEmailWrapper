using System;
using System.Drawing;
using System.Windows.Forms;
using AowEmailWrapper.Helpers;
using AowEmailWrapper.Localization;

namespace AowEmailWrapper.Classes
{
    public class InputBox
    {
        private const string ButtonKeyOK = "buttonOK";
        private const string ButtonKeyCancel = "buttonCancel";

        public static DialogResult Show(string title, string promptText, ref string value)
        {
            return ShowInput(title, promptText, ref value, null);
        }

        public static DialogResult Show(string title, string promptText, ref string value, Image iconImage)
        {
            Icon theIcon = FlimFlan.IconEncoder.Converter.BitmapToIcon(iconImage as Bitmap);
            return ShowInput(title, promptText, ref value, theIcon);
        }

        public static DialogResult Show(string title, string promptText, ref string value, Icon icon)
        {
            return ShowInput(title, promptText, ref value, icon);
        }

        private static DialogResult ShowInput(string title, string promptText, ref string value, Icon icon)
        {
            DialogResult dialogResult;

            using (Form form = new Form())
            {
                if (icon != null)
                {
                    form.Icon = icon;
                }

                Label label = new Label();
                TextBox textBox = new TextBox();
                Button buttonOk = new Button();
                Button buttonCancel = new Button();
                
                form.Text = title;
                label.Text = promptText;
                textBox.Text = value;

                buttonOk.Text = Translator.Translate(ButtonKeyOK);
                buttonCancel.Text = Translator.Translate(ButtonKeyCancel);
                buttonOk.DialogResult = DialogResult.OK;
                buttonCancel.DialogResult = DialogResult.Cancel;

                //Laid out in 96 dpi units and scaled to the screen
                Func<int, int> scaled = value => DpiHelper.Scale(value);
                label.SetBounds(scaled(9), scaled(20), scaled(372), scaled(13));
                textBox.SetBounds(scaled(12), scaled(36), scaled(372), scaled(20));
                buttonOk.SetBounds(scaled(228), scaled(72), scaled(75), scaled(23));
                buttonCancel.SetBounds(scaled(309), scaled(72), scaled(75), scaled(23));

                label.AutoSize = true;
                textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
                buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

                form.ShowInTaskbar = false;
                form.ClientSize = new Size(scaled(396), scaled(107));
                form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
                form.ClientSize = new Size(Math.Max(scaled(300), label.Right + scaled(10)), form.ClientSize.Height);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.AcceptButton = buttonOk;
                form.CancelButton = buttonCancel;
                dialogResult = form.ShowDialog();

                if (dialogResult.Equals(DialogResult.OK))
                {
                    value = textBox.Text;
                }

                label.Dispose();
                textBox.Dispose();
                buttonOk.Dispose();
                buttonCancel.Dispose();
            }

            return dialogResult;
        }
    }
}
