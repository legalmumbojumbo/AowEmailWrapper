using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AowEmailWrapper.Classes;
using AowEmailWrapper.ConfigFramework;
using AowEmailWrapper.Games;
using AowEmailWrapper.Localization;

namespace AowEmailWrapper.Controls
{
    /// <summary>
    /// The Games tab: every copy of every game the Wrapper found, with the label each copy sends
    /// its turns under and which copy is the default for turns that carry no label. Folders the
    /// detection misses can be added by hand.
    /// </summary>
    public class GamesConfig : UserControl
    {
        private const string NoGameInFolderKey = "msgAddFolderNoGame";
        private const string MissingKey = "msgInstallMissing";
        private const string ScanningKey = "buttonRescanning";
        private const string DefaultMark = "✓";
        private const int ButtonPanelWidth = 113;
        private const int ButtonHeight = 42;

        private readonly Panel panelGames;
        private readonly ListView listViewGames;
        private readonly Panel panelButtons;
        private readonly Button buttonAddFolder;
        private readonly Button buttonSetLabel;
        private readonly Button buttonSetDefaultInstall;
        private readonly Button buttonRemoveInstall;
        private readonly Button buttonRescan;
        private readonly Label lblGamesHelp;

        private List<AowGame> _games = new List<AowGame>();
        private bool _resizing;
        private bool _scanning;

        public EventHandler Config_Changed;

        /// <summary>Supplies the detected copies; the tab works on its own copy of the list until settings are saved.</summary>
        public AowGameManager GameManager { get; set; }

        public GamesConfig()
        {
            Name = "GamesConfig";
            Padding = new Padding(5);

            lblGamesHelp = new Label();
            lblGamesHelp.Name = "lblGamesHelp";
            lblGamesHelp.Dock = DockStyle.Bottom;
            lblGamesHelp.Height = 78;
            lblGamesHelp.Padding = new Padding(0, 8, 0, 0);
            lblGamesHelp.Text = "Give each copy of a game a short label such as Vanilla, AoWx or Ziggurat. The label travels with the turns you send, so everyone in a game must use the same label. The default copy receives turns that carry no label.";

            panelGames = new Panel();
            panelGames.Name = "panelGames";
            panelGames.Dock = DockStyle.Fill;

            listViewGames = new ListView();
            listViewGames.Name = "listViewGames";
            listViewGames.Dock = DockStyle.Fill;
            listViewGames.View = View.Details;
            listViewGames.FullRowSelect = true;
            listViewGames.MultiSelect = false;
            listViewGames.HideSelection = false;
            listViewGames.ShowItemToolTips = true;
            listViewGames.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            listViewGames.Columns.Add(new ColumnHeader { Text = "Game", Tag = "ContentHeaderMax" });
            listViewGames.Columns.Add(new ColumnHeader { Text = "Label", Tag = "ContentHeaderMax" });
            listViewGames.Columns.Add(new ColumnHeader { Text = "Folder", Tag = "Fill" });
            listViewGames.Columns.Add(new ColumnHeader { Text = "Default", Tag = "HeaderSize" });
            listViewGames.Columns.Add(new ColumnHeader { Text = "Found by", Tag = "ContentHeaderMax" });
            listViewGames.SelectedIndexChanged += (sender, e) => UpdateButtons();
            //Sized on control resize only: reacting to the list's own client size changes loops when a scroll bar appears
            Resize += (sender, e) => FitColumns();
            listViewGames.DoubleClick += (sender, e) => SetLabel();

            panelButtons = new Panel();
            panelButtons.Dock = DockStyle.Right;
            panelButtons.Width = ButtonPanelWidth;
            panelButtons.Padding = new Padding(5, 0, 0, 0);

            //Docked Top, so the last one added ends up at the top
            buttonRescan = AddButton("buttonRescan", "Rescan", (sender, e) => Rescan());
            buttonRemoveInstall = AddButton("buttonRemoveInstall", "Remove", (sender, e) => RemoveSelected());
            buttonSetDefaultInstall = AddButton("buttonSetDefaultInstall", "Set as default", (sender, e) => SetDefault());
            buttonSetLabel = AddButton("buttonSetLabel", "Set label...", (sender, e) => SetLabel());
            buttonAddFolder = AddButton("buttonAddFolder", "Add folder...", (sender, e) => AddFolder());

            panelGames.Controls.Add(listViewGames);
            panelGames.Controls.Add(panelButtons);

            Controls.Add(panelGames);
            Controls.Add(lblGamesHelp);

            UpdateButtons();
        }

        /// <summary>The list as config entries; setting it re-reads the detected copies from the game manager.</summary>
        public GamesConfigValues Config
        {
            get
            {
                GamesConfigValues config = new GamesConfigValues();
                foreach (AowGame game in _games)
                {
                    config.Installs.Add(new GameInstallConfigValues(game));
                }
                return config;
            }
            set
            {
                IEnumerable<AowGame> source = GameManager != null
                    ? GameManager.Games
                    : new AowGameManager(null, value).Games;
                _games = source.Select(Clone).ToList();
                Populate();
            }
        }

        private Button AddButton(string name, string text, EventHandler onClick)
        {
            Panel holder = new Panel();
            holder.Dock = DockStyle.Top;
            holder.Height = ButtonHeight;
            holder.Padding = new Padding(0, 0, 0, 5);

            Button button = new Button();
            button.Name = name;
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.UseVisualStyleBackColor = true;
            button.Click += onClick;

            holder.Controls.Add(button);
            panelButtons.Controls.Add(holder);
            return button;
        }

        private static AowGame Clone(AowGame game)
        {
            AowGame copy = new AowGame(game.GameType, game.Folder, game.Source);
            copy.Label = game.Label;
            copy.IsDefault = game.IsDefault;
            return copy;
        }

        private AowGame Selected
        {
            get { return listViewGames.SelectedItems.Count == 1 ? listViewGames.SelectedItems[0].Tag as AowGame : null; }
        }

        private void Populate()
        {
            AowGame selected = Selected;

            listViewGames.BeginUpdate();
            listViewGames.Items.Clear();

            foreach (AowGame game in _games.OrderBy(game => game.GameType).ThenBy(game => game.IsDefault ? 0 : 1).ThenBy(game => game.Folder))
            {
                ListViewItem item = new ListViewItem(AowGame.DisplayNameFor(game.GameType));
                item.SubItems.Add(game.Label);
                item.SubItems.Add(game.Folder);
                item.SubItems.Add(game.IsDefault ? DefaultMark : string.Empty);
                item.SubItems.Add(game.IsInstalled ? Translator.TranslateEnum(game.Source) : Translator.Translate(MissingKey));
                item.ToolTipText = game.Folder;
                item.Tag = game;
                if (!game.IsInstalled)
                {
                    item.ForeColor = SystemColors.GrayText;
                }
                if (selected != null && selected.Id == game.Id)
                {
                    item.Selected = true;
                }
                listViewGames.Items.Add(item);
            }

            listViewGames.EndUpdate();
            FitColumns();
            UpdateButtons();
        }

        private void FitColumns()
        {
            if (_resizing)
            {
                return;
            }
            _resizing = true;
            try
            {
                listViewGames.BeginUpdate();
                ListViewColumnResizer.ResizeColumns(listViewGames);
            }
            finally
            {
                listViewGames.EndUpdate();
                _resizing = false;
            }
        }

        private void UpdateButtons()
        {
            AowGame selected = Selected;
            buttonSetLabel.Enabled = selected != null;
            buttonSetDefaultInstall.Enabled = selected != null && selected.IsInstalled && !selected.IsDefault;
            buttonRemoveInstall.Enabled = selected != null && selected.IsManual;
        }

        private void RaiseChanged()
        {
            if (Config_Changed != null)
            {
                Config_Changed(this, EventArgs.Empty);
            }
        }

        private void SetLabel()
        {
            AowGame game = Selected;
            if (game == null)
            {
                return;
            }

            Dictionary<string, string> taken = new Dictionary<string, string>();
            foreach (AowGame other in _games.Where(other => other.GameType == game.GameType && other.Id != game.Id && !string.IsNullOrEmpty(other.Label)))
            {
                taken[other.Label] = other.Folder;
            }
            string label = LabelDialog.Show(this, game, taken);
            if (label != null)
            {
                game.Label = label;
                Populate();
                RaiseChanged();
            }
        }

        private void SetDefault()
        {
            AowGame game = Selected;
            if (game == null || !game.IsInstalled)
            {
                return;
            }

            foreach (AowGame other in _games.Where(other => other.GameType == game.GameType))
            {
                other.IsDefault = false;
            }
            game.IsDefault = true;
            Populate();
            RaiseChanged();
        }

        private void RemoveSelected()
        {
            AowGame game = Selected;
            if (game == null || !game.IsManual)
            {
                return;
            }

            _games.Remove(game);
            EnsureDefaults();
            Populate();
            RaiseChanged();
        }

        private void AddFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                List<AowGame> found = GameDetector.ScanFolder(dialog.SelectedPath, InstallSource.Manual);
                if (found.Count == 0)
                {
                    MessageBox.Show(this, Translator.Translate(NoGameInFolderKey, dialog.SelectedPath), Translator.Translate("Main"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                bool added = false;
                foreach (AowGame game in found)
                {
                    AowGame existing = _games.FirstOrDefault(other => other.GameType == game.GameType && other.IsFolder(game.Folder));
                    if (existing == null)
                    {
                        _games.Add(game);
                        added = true;
                    }
                }

                if (added)
                {
                    EnsureDefaults();
                    Populate();
                    RaiseChanged();
                }
            }
        }

        /// <summary>Runs the full drive scan again in the background, keeping the labels and defaults set so far.</summary>
        private async void Rescan()
        {
            if (_scanning)
            {
                return;
            }
            _scanning = true;

            GamesConfigValues known = Config;
            string originalText = buttonRescan.Text;
            buttonRescan.Enabled = false;
            buttonRescan.Text = Translator.Translate(ScanningKey);

            try
            {
                List<AowGame> detected = await Task.Run(() => GameDetector.Detect(known.Installs, true));
                AowGameManager fresh = new AowGameManager(GameManager != null ? GameManager.CheckEmailFolder : null, detected, known);
                _games = fresh.Games.Select(Clone).ToList();
                Populate();
                RaiseChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Rescan failed: {0}", ex);
            }
            finally
            {
                buttonRescan.Text = originalText;
                buttonRescan.Enabled = true;
                _scanning = false;
            }
        }

        private void EnsureDefaults()
        {
            foreach (AowGameType type in AowGame.AllTypes)
            {
                List<AowGame> installed = _games.Where(game => game.GameType == type && game.IsInstalled).ToList();
                if (installed.Count > 0 && !installed.Any(game => game.IsDefault))
                {
                    installed.OrderBy(game => game.Source).First().IsDefault = true;
                }
            }
        }
    }
}
