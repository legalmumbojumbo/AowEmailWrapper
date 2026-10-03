using System;
using AowEmailWrapper.Helpers;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AowEmailWrapper.Games;
using AowEmailWrapper.Localization;

namespace AowEmailWrapper.Controls
{
    /// <summary>
    /// Picks the label for a copy of a game: one radio button per known label, "No label", and
    /// "Other" with a text box for anything else.
    /// </summary>
    public class LabelDialog : Form
    {
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Theme.Apply(this);
        }

        private const string NoLabelKey = "radioNoLabel";
        private const string OtherKey = "radioOtherLabel";
        private const string OkKey = "buttonOK";
        private const string CancelKey = "buttonCancel";
        private const string MoveHintKey = "msgLabelMoveHint";
        private const string MoveHintFallback = "Choosing a label another copy uses moves it to this copy.";
        private const int Pad = 16;
        private const int RowHeight = 26;

        //Mods the community plays; the labels of other copies on this PC are offered as well
        private static readonly Dictionary<AowGameType, string[]> Presets = new Dictionary<AowGameType, string[]>
        {
            { AowGameType.Aow1, new[] { ModDetector.Vanilla, ModDetector.Evolved, ModDetector.AowX, ModDetector.Ziggurat, ModDetector.DarkLord } },
            { AowGameType.Aow2, new[] { "Vanilla" } },
            { AowGameType.AowSm, new[] { "Vanilla" } },
            { AowGameType.AowMpe, new[] { "Vanilla" } },
        };

        private readonly List<RadioButton> _choices = new List<RadioButton>();
        private readonly RadioButton _other;
        private readonly TextBox _otherText;
        private string _result;

        /// <summary>
        /// Returns the chosen label (empty for none), or null when cancelled. Labels already used by
        /// another copy of the same game are offered like any other: several copies may share one
        /// nor accepted, so every label points at exactly one copy.
        /// </summary>
        public static string Show(IWin32Window owner, AowGame game)
        {
            using (LabelDialog dialog = new LabelDialog(game.DisplayName, game.Label, BuildOptions(game)))
            {
                dialog.ShowDialog(owner);
                return dialog._result;
            }
        }

        /// <summary>
        /// The choices to offer, text to label: what the folder's contents call for first, then the
        /// presets and the current label.
        /// </summary>
        public static List<KeyValuePair<string, string>> BuildOptions(AowGame game)
        {
            List<string> labels = new List<string>();
            string[] presets;
            if (Presets.TryGetValue(game.GameType, out presets))
            {
                labels.AddRange(presets);
            }
            labels.RemoveAll(option => AowGame.SameLabel(option, game.SuggestedLabel));
            labels.Insert(0, game.SuggestedLabel);
            if (!string.IsNullOrWhiteSpace(game.Label))
            {
                labels.Add(game.Label.Trim());
            }

            List<KeyValuePair<string, string>> options = new List<KeyValuePair<string, string>>();
            foreach (string label in labels.GroupBy(AowGame.NormalizeLabel).Select(group => group.First()))
            {
                options.Add(new KeyValuePair<string, string>(label, label));
            }
            return options;
        }

        private LabelDialog(string title, string current, List<KeyValuePair<string, string>> options)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            //Laid out in 96 dpi units and scaled to the screen; text heights are measured in the theme's font
            int pad = DpiHelper.Scale(Pad);
            int rowHeight = DpiHelper.Scale(RowHeight);
            int width = DpiHelper.Scale(360);
            Font measureFont = DpiHelper.MeasureFont(this);
            int y = pad;

            RadioButton none = AddChoice(Translator.Translate(NoLabelKey), string.Empty, ref y);
            foreach (KeyValuePair<string, string> option in options)
            {
                AddChoice(option.Key, option.Value, ref y);
            }

            _other = new RadioButton();
            _other.Text = Translator.Translate(OtherKey);
            _other.AutoSize = true;
            _other.Location = new Point(pad, y + DpiHelper.Scale(3));
            Controls.Add(_other);

            //The text box starts after the "Other" caption, however wide the translation and the font make it
            int otherWidth = TextRenderer.MeasureText(_other.Text, measureFont).Width + DpiHelper.Scale(28);
            int textLeft = pad + Math.Max(DpiHelper.Scale(90), otherWidth);
            _otherText = new TextBox();
            _otherText.Location = new Point(textLeft, y + DpiHelper.Scale(1));
            _otherText.Width = width - textLeft - pad;
            _otherText.MaxLength = 40;
            _otherText.TextChanged += (sender, e) => { if (_otherText.Text.Length > 0) _other.Checked = true; };
            _otherText.Enter += (sender, e) => _other.Checked = true;
            Controls.Add(_otherText);
            y += rowHeight + pad;

            if (options.Any(option => option.Key != option.Value))
            {
                Label hint = new Label();
                string hintText = Translator.Translate(MoveHintKey);
                hint.Text = string.IsNullOrEmpty(hintText) ? MoveHintFallback : hintText;
                hint.AutoSize = false;
                hint.Location = new Point(pad, y);
                int hintHeight = TextRenderer.MeasureText(hint.Text, measureFont, new Size(width - pad * 2, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                hint.Size = new Size(width - pad * 2, Math.Max(rowHeight, hintHeight + DpiHelper.Scale(4)));
                hint.ForeColor = SystemColors.GrayText;
                Controls.Add(hint);
                y += hint.Height + pad / 2;
            }

            Button ok = new Button();
            ok.Text = Translator.Translate(OkKey);
            ok.Size = DpiHelper.Scale(84, 26);
            ok.Location = new Point(width - pad - ok.Width * 2 - DpiHelper.Scale(8), y);
            ok.Click += (sender, e) =>
            {
                if (Accept())
                {
                    DialogResult = DialogResult.OK;
                }
            };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = Translator.Translate(CancelKey);
            cancel.Size = ok.Size;
            cancel.Location = new Point(width - pad - cancel.Width, y);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            ClientSize = new Size(width, y + ok.Height + pad);

            //Pre-select the current label
            RadioButton match = _choices.FirstOrDefault(choice => AowGame.SameLabel((string)choice.Tag, current) && !string.IsNullOrEmpty(current));
            if (match != null)
            {
                match.Checked = true;
            }
            else if (!string.IsNullOrEmpty(current))
            {
                _other.Checked = true;
                _otherText.Text = current;
            }
            else
            {
                none.Checked = true;
            }
        }

        private RadioButton AddChoice(string text, string value, ref int y)
        {
            RadioButton choice = new RadioButton();
            choice.Text = text;
            choice.Tag = value;
            choice.AutoSize = true;
            choice.Location = new Point(DpiHelper.Scale(Pad), y);
            choice.DoubleClick += (sender, e) => { if (Accept()) DialogResult = DialogResult.OK; };
            Controls.Add(choice);
            _choices.Add(choice);
            y += DpiHelper.Scale(RowHeight);
            return choice;
        }

        /// <summary>Reads the choice. A label another copy holds is accepted: the caller moves it.</summary>
        private bool Accept()
        {
            if (_other.Checked)
            {
                _result = _otherText.Text.Trim();
            }
            else
            {
                RadioButton chosen = _choices.FirstOrDefault(choice => choice.Checked);
                _result = chosen != null ? (string)chosen.Tag : string.Empty;
            }
            return true;
        }
    }
}
