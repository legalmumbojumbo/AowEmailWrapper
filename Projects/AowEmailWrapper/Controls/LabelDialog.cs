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
        private const string HeldByKey = "msgLabelHeldBy";
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
        /// another copy of the same game (taken: label to that copy's folder) are neither offered
        /// nor accepted, so every label points at exactly one copy.
        /// </summary>
        public static string Show(IWin32Window owner, AowGame game, IDictionary<string, string> taken)
        {
            using (LabelDialog dialog = new LabelDialog(game.DisplayName, game.Label, BuildOptions(game, taken)))
            {
                dialog.ShowDialog(owner);
                return dialog._result;
            }
        }

        /// <summary>
        /// The choices to offer, text to label: what the folder's contents call for first, then the
        /// presets and the current label. A label another copy holds is still offered, marked with
        /// that copy's folder, and choosing it moves the label to this copy.
        /// </summary>
        public static List<KeyValuePair<string, string>> BuildOptions(AowGame game, IDictionary<string, string> taken)
        {
            taken = taken ?? new Dictionary<string, string>();
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
                string holder = taken.Where(pair => AowGame.SameLabel(pair.Key, label)).Select(pair => pair.Value).FirstOrDefault();
                string text = holder == null ? label : string.Format("{0} ({1})", label, HeldBy(FolderName(holder)));
                options.Add(new KeyValuePair<string, string>(text, label));
            }
            return options;
        }

        /// <summary>"now on Age of Wonders zig", with an English fallback when no language table is loaded.</summary>
        private static string HeldBy(string folderName)
        {
            string text = Translator.Translate(HeldByKey, folderName);
            return string.IsNullOrEmpty(text) ? string.Format("used by {0}", folderName) : text;
        }

        private static string FolderName(string folder)
        {
            string trimmed = (folder ?? string.Empty).TrimEnd('\\', '/');
            int cut = trimmed.LastIndexOfAny(new[] { '\\', '/' });
            return cut >= 0 ? trimmed.Substring(cut + 1) : trimmed;
        }

        private LabelDialog(string title, string current, List<KeyValuePair<string, string>> options)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            int width = 360;
            int y = Pad;

            RadioButton none = AddChoice(Translator.Translate(NoLabelKey), string.Empty, ref y);
            foreach (KeyValuePair<string, string> option in options)
            {
                AddChoice(option.Key, option.Value, ref y);
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

            if (options.Any(option => option.Key != option.Value))
            {
                Label hint = new Label();
                string hintText = Translator.Translate(MoveHintKey);
                hint.Text = string.IsNullOrEmpty(hintText) ? MoveHintFallback : hintText;
                hint.AutoSize = false;
                hint.Location = new Point(Pad, y);
                hint.Size = new Size(width - Pad * 2, RowHeight);
                hint.ForeColor = SystemColors.GrayText;
                Controls.Add(hint);
                y += RowHeight + Pad / 2;
            }

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

        private RadioButton AddChoice(string text, string value, ref int y)
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
