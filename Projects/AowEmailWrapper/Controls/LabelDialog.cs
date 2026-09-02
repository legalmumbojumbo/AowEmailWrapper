using System;
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
        private const string NoLabelKey = "radioNoLabel";
        private const string OtherKey = "radioOtherLabel";
        private const string OkKey = "buttonOK";
        private const string CancelKey = "buttonCancel";
        private const string InUseKey = "msgLabelInUse";
        private const int Pad = 16;
        private const int RowHeight = 26;

        //Mods the community plays; the labels of other copies on this PC are offered as well
        private static readonly Dictionary<AowGameType, string[]> Presets = new Dictionary<AowGameType, string[]>
        {
            { AowGameType.Aow1, new[] { "Vanilla", "AoWx", "Ziggurat" } },
            { AowGameType.Aow2, new[] { "Vanilla" } },
            { AowGameType.AowSm, new[] { "Vanilla" } },
            { AowGameType.AowMpe, new[] { "Vanilla" } },
        };

        private readonly List<RadioButton> _choices = new List<RadioButton>();
        private readonly RadioButton _other;
        private readonly TextBox _otherText;
        private readonly IDictionary<string, string> _taken;
        private string _result;

        /// <summary>
        /// Returns the chosen label (empty for none), or null when cancelled. Labels already used by
        /// another copy of the same game (taken: label to that copy's folder) are neither offered
        /// nor accepted, so every label points at exactly one copy.
        /// </summary>
        public static string Show(IWin32Window owner, AowGame game, IDictionary<string, string> taken)
        {
            taken = taken ?? new Dictionary<string, string>();
            List<string> options = new List<string>();
            string[] presets;
            if (Presets.TryGetValue(game.GameType, out presets))
            {
                options.AddRange(presets);
            }
            if (!string.IsNullOrWhiteSpace(game.Label))
            {
                options.Add(game.Label.Trim());
            }
            options = options.Where(option => !taken.Keys.Any(used => AowGame.SameLabel(used, option)))
                             .GroupBy(AowGame.NormalizeLabel).Select(group => group.First()).ToList();

            using (LabelDialog dialog = new LabelDialog(game.DisplayName, game.Label, options, taken))
            {
                dialog.ShowDialog(owner);
                return dialog._result;
            }
        }

        private LabelDialog(string title, string current, List<string> options, IDictionary<string, string> taken)
        {
            _taken = taken;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            int width = 360;
            int y = Pad;

            RadioButton none = AddChoice(Translator.Translate(NoLabelKey), string.Empty, ref y, width);
            foreach (string option in options)
            {
                AddChoice(option, option, ref y, width);
            }

            _other = new RadioButton();
            _other.Text = Translator.Translate(OtherKey);
            _other.AutoSize = true;
            _other.Location = new Point(Pad, y + 3);
            Controls.Add(_other);

            _otherText = new TextBox();
            _otherText.Location = new Point(Pad + 90, y + 1);
            _otherText.Width = width - Pad - 90 - Pad;
            _otherText.MaxLength = 40;
            _otherText.TextChanged += (sender, e) => { if (_otherText.Text.Length > 0) _other.Checked = true; };
            _otherText.Enter += (sender, e) => _other.Checked = true;
            Controls.Add(_otherText);
            y += RowHeight + Pad;

            Button ok = new Button();
            ok.Text = Translator.Translate(OkKey);
            ok.Size = new Size(84, 26);
            ok.Location = new Point(width - Pad - ok.Width * 2 - 8, y);
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
            cancel.Location = new Point(width - Pad - cancel.Width, y);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            ClientSize = new Size(width, y + ok.Height + Pad);

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

        private RadioButton AddChoice(string text, string value, ref int y, int width)
        {
            RadioButton choice = new RadioButton();
            choice.Text = text;
            choice.Tag = value;
            choice.AutoSize = true;
            choice.Location = new Point(Pad, y);
            choice.DoubleClick += (sender, e) => { if (Accept()) DialogResult = DialogResult.OK; };
            Controls.Add(choice);
            _choices.Add(choice);
            y += RowHeight;
            return choice;
        }

        /// <summary>Reads the choice; false (with a message) when the label belongs to another copy.</summary>
        private bool Accept()
        {
            string label;
            if (_other.Checked)
            {
                label = _otherText.Text.Trim();
            }
            else
            {
                RadioButton chosen = _choices.FirstOrDefault(choice => choice.Checked);
                label = chosen != null ? (string)chosen.Tag : string.Empty;
            }

            if (!string.IsNullOrEmpty(label))
            {
                string usedBy = _taken.Where(pair => AowGame.SameLabel(pair.Key, label)).Select(pair => pair.Value).FirstOrDefault();
                if (usedBy != null)
                {
                    MessageBox.Show(this, Translator.Translate(InUseKey, label, usedBy), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _result = null;
                    return false;
                }
            }

            _result = label;
            return true;
        }
    }
}
