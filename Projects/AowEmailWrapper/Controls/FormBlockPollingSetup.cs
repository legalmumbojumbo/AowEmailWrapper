using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AowEmailWrapper.Classes;
using AowEmailWrapper.Localization;

namespace AowEmailWrapper.Controls
{
    public partial class FormBlockPollingSetup : BaseFormBlock
    {
        private const string MinutesTextKey = "enumMinutes";
        private const string DisplayTextTemplate = "{0} {1}";
        private const int CheckBoxTickWidth = 24;

        public FormBlockPollingSetup()
        {
            InitializeComponent();
            comboBox.DisplayMember = "Text";
            base.SetControls(panelLabel, comboBox);
        }

        public string CheckBoxText
        {
            get { return checkBox.Text; }
            set { checkBox.Text = value; }
        }

        public string EveryText
        {
            get { return labelEvery.Text; }
            set { labelEvery.Text = value; }
        }

        protected override void OnResize(EventArgs e)
        {
            //The base block sizes itself from its label text, which this row does not have, so it lays itself out
            if (resizing)
            {
                return;
            }

            try
            {
                resizing = true;
                SuspendLayout();

                int third = (int)Math.Ceiling(Width / 3.0);
                comboBox.Width = Math.Max(60, third * 2 - 20);

                LayoutRow();
                ResumeLayout(true);
            }
            finally
            {
                resizing = false;
            }
        }

        /// <summary>
        /// One line: the check box, the "every:" label and the interval box share the combo's height and centre line.
        /// </summary>
        private void LayoutRow()
        {
            if (comboBox == null || panelLabel == null || panelEvery == null)
            {
                return;
            }

            int rowHeight = comboBox.Height + Padding.Vertical;
            if (Height != rowHeight)
            {
                MinimumSize = new Size(0, rowHeight);
                Height = rowHeight;
            }

            int top = Padding.Top;
            int lineHeight = comboBox.Height;

            //Interval box on the right, everything else on the same line to its left
            comboBox.Location = new Point(Math.Max(Padding.Left, Width - Padding.Right - comboBox.Width), top);

            panelLabel.AutoSize = false;
            panelLabel.Bounds = new Rectangle(Padding.Left, top, Math.Max(0, comboBox.Left - Padding.Left), lineHeight);

            Size everySize = TextRenderer.MeasureText(labelEvery.Text, labelEvery.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine);
            int everyWidth = everySize.Width + 6;
            panelEvery.Bounds = new Rectangle(Math.Max(0, panelLabel.Width - everyWidth), 0, everyWidth, lineHeight);
            //Designer minimum sizes get scaled with the font and would otherwise force these taller than the line
            labelEvery.AutoSize = false;
            labelEvery.MinimumSize = Size.Empty;
            labelEvery.MaximumSize = Size.Empty;
            labelEvery.Bounds = new Rectangle(0, 0, everyWidth, lineHeight);

            checkBox.AutoSize = false;
            checkBox.MinimumSize = Size.Empty;
            checkBox.MaximumSize = Size.Empty;
            int spareWidth = Math.Max(CheckBoxTickWidth, panelEvery.Left);
            Size checkBoxText = TextRenderer.MeasureText(checkBox.Text, checkBox.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine);
            checkBox.Bounds = new Rectangle(0, 0, Math.Min(spareWidth, checkBoxText.Width + CheckBoxTickWidth), lineHeight);
        }

        public void AddItem(int value)
        {
            AddItem(new ComboBoxItem(value.ToString(), string.Format(DisplayTextTemplate, value, Translator.Translate(MinutesTextKey))));
        }

        private void AddItem(ComboBoxItem theItem)
        {
            comboBox.Items.Add(theItem);
        }

        public string SelectedValue
        {
            get
            {
                if (comboBox.SelectedIndex >= 0)
                {
                    ComboBoxItem theSelectedItem = (ComboBoxItem)comboBox.SelectedItem;
                    return theSelectedItem.Value;
                }
                else
                {
                    return string.Empty;
                }
            }
            set
            {
                foreach (ComboBoxItem item in comboBox.Items)
                {
                    if (item.Value.Equals(value, StringComparison.InvariantCultureIgnoreCase))
                    {
                        comboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        public int SelectedIndex
        {
            get { return comboBox.SelectedIndex; }
            set { comboBox.SelectedIndex = value; }
        }

        public ComboBox.ObjectCollection Items
        {
            get { return comboBox.Items; }
        }

        public ComboBox InnerComboBox
        {
            get { return comboBox; }
        }

        public bool Checked
        {
            get { return checkBox.Checked; }
            set { checkBox.Checked = value; }
        }

        public CheckBox InnerCheckBox
        {
            get { return checkBox; }
        }
    }
}
