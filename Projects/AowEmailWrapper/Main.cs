using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using EricDaugherty.CSES.Net;
using EricDaugherty.CSES.SmtpServer;
using AowEmailWrapper.ASG;
using AowEmailWrapper.CSES;
using AowEmailWrapper.Classes;
using AowEmailWrapper.ConfigFramework;
using Activity = AowEmailWrapper.ConfigFramework.Activity;
using AowEmailWrapper.Controls;
using AowEmailWrapper.Pollers;
using AowEmailWrapper.Games;
using AowEmailWrapper.Helpers;
using AowEmailWrapper.Localization;

using MimeKit;

using Microsoft.Win32;

namespace AowEmailWrapper
{
    public enum IconState
    { 
        None = 1,
        Normal,
        Sending,
        Checking,
        EmailWaiting,
        CheckEmail
    }

    public partial class Main : Form
    {
        #region String Constants

        private const string WINDOWS_REG_STARTUP_LOCATION = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string WrapperWriteAccessMessageBoxKey = "msgWrapperWriteAccess";
        private const string WrapperFixPermissionsKey = "msgWrapperFixPermissions";
        private const string WrapperFixPermissionsDoneKey = "msgWrapperFixPermissionsDone";
        private const string WrapperFixPermissionsFailedKey = "msgWrapperFixPermissionsFailed";
        private const string WrapperArchiveGameMessageBoxKey = "msgWrapperArchiveGame";
        private const string WrapperCannotActivateAccountMessageBoxKey = "msgWrapperCannotActivateAccount";
        private const string WrapperEmailSentSuccessKey = "msgWrapperEmailSentSuccess";
        private const string WrapperEmailSentFailedKey = "msgWrapperEmailSentFailed";
        private const string WrapperRestartRequiredKey = "msgWrapperRestartRequired";
        private const string WrapperPollAuthFailedKey = "msgWrapperPollAuthFailed";
        private const string WrapperPollFailedKey = "msgWrapperPollFailed";
        private const string WrapperGamesWaitingKey = "msgWrapperGamesWaiting";
        private const string WrapperResendToKey = "msgWrapperResendTo";
        private const string WrapperUpdateAvailableKey = "msgWrapperUpdateAvailable";
        private const string WrapperUpdateBalloonKey = "msgWrapperUpdateBalloon";
        private const string WrapperUpdateOfferKey = "msgWrapperUpdateOffer";
        private const string WrapperUpdateNoneKey = "msgWrapperUpdateNone";
        private const string WrapperUpdateFailedKey = "msgWrapperUpdateFailed";
        private const string WrapperUpdateInstallingKey = "msgWrapperUpdateInstalling";
        private const string MsgDocumentMissingKey = "msgDocumentMissing";
        private const string LinkUpdateCheckingKey = "linkUpdateChecking";
        private const string LinkUpdateAvailableKey = "linkUpdateAvailable";

        private const string MainFormTitleTemplate = "{0} - {1}";
        private const string MainFormTitleMoreTemplate = "{0} - {1} (+{2})";
        private const string WrapperNoSendAccountKey = "msgWrapperNoSendAccount";

        private const string Menu_Show_Tag = "menuItemShow";
        private const string Menu_Accounts_Tag = "menuItemAccounts";
        private const string Menu_Poll_Tag = "menuItemPollNow";
        private const string Menu_Exit_Tag = "menuItemExit";
        private const string GameMenuTagPrefix = "game:";

        private const string GameSmtpServerTemplate = "127.0.0.1:{0}";
        private const string WrapperAutostartTemplate = "\"{0}\" {1}";
        private const string WrapperRestartTemplate = "{0} {1}";

        private const string ButtonKeyOK = "buttonOK";
        private const string ButtonKeyCancel = "buttonCancel";
        private const string ButtonKeyResend = "buttonResend";

        #endregion

        #region Private Members

        private Icon _baseIcon = null;
        private SimpleServer _theServer;
        private readonly Dictionary<string, BasePoller> _pollers = new Dictionary<string, BasePoller>();
        private static AowGameManager _gameManager;
        private readonly Dictionary<string, SmtpSender> _senders = new Dictionary<string, SmtpSender>();
        //Message id of a turn being sent -> folder of the copy of the game it came from
        private readonly Dictionary<string, string> _outgoingInstalls = new Dictionary<string, string>();

        private Config _wrapperConfig;
        private ActivityList _activityLog;

        private StartedTaskWatcher _aow1GameWatcher;
        private StartedTaskWatcher _aow2GameWatcher;
        private StartedTaskWatcher _aowSmGameWatcher;

        private EventHandler _shutDownEvent;
        private EventHandler _maximizeEvent;
        private EventHandler _activityLogRefresh;
        private bool _closeCancel = true;

        //Automatic installs happen right after start-up, before anything is in flight; a check that
        //only announces waits until the pollers and the games have settled
        private const int UpdateInstallDelayMilliseconds = 3000;
        private const int UpdateCheckDelayMilliseconds = 20000;
        private const int UpdateNotesMaxLength = 600;
        private System.Windows.Forms.Timer _updateTimer;
        private UpdateInfo _availableUpdate;
        private bool _updateCheckRunning;
        private bool _updateBalloonShown;
        private bool _isNewConfig = false;
        private bool _configNeedsSave = false;
        private bool _configChangeTracking = false;
        private int _showingExceptionCount = 0;

        private ContextMenuStrip _contextMenu;

        private ToolStripMenuItem _menuAccounts;
        private ToolStripMenuItem _menuShow;
        private ToolStripMenuItem _menuPoll;
        private ToolStripMenuItem _menuExit;

        #endregion

        #region Properties
        
        //To hide from alt-tab when minimized
        protected override CreateParams CreateParams
        {
            get
            {
                //Turn off WS_EX_CONTROLPARENT style bit
                CreateParams cp = base.CreateParams;
                int bit = ExtendedWindowStyles.WS_EX_CONTROLPARENT;
                int test = cp.ExStyle & bit;
                if (test.Equals(bit))
                {
                    cp.ExStyle ^= bit;
                }
                return cp;
            }
        }

        protected bool ConfigNeedsSave
        {
            get { return _configNeedsSave; }
            set
            {
                _configNeedsSave = value;
                cmdSave.Enabled = _configNeedsSave;
                cmdSave.BackColor = cmdSave.Enabled ? Color.IndianRed : this.BackColor;
            }
        }

        protected bool ConfigChangeTracking
        {
            get { return _configChangeTracking; }
            set
            {
                _configChangeTracking = value;
                EventHandler configNeedsSave = new EventHandler(OnConfigNeedsSave);

                if (_configChangeTracking)
                {
                    accountsConfig.Config_Changed += configNeedsSave;
                    preferencesConfig.Config_Changed += configNeedsSave;
                    gamesConfig.Config_Changed += configNeedsSave;
                }
                else
                {
                    accountsConfig.Config_Changed -= configNeedsSave;
                    preferencesConfig.Config_Changed -= configNeedsSave;
                    gamesConfig.Config_Changed -= configNeedsSave;
                }
            }
        }

        //The Wrapper Exe is left in memory if we shut down while an exception dialog is up
        protected bool OkayToShutDown
        {
            get { return _showingExceptionCount.Equals(0); }
        }

        #endregion

        public Main()
        {
            _wrapperConfig = DataManagerHelper.LoadConfig(out _isNewConfig);

            LoadTranslations();

            InitializeComponent();
            ImageListLoader.Load(imageListIcons, "Main");

            Translator.TranslateForm(this);

            //The wrapped text in these boxes is only as tall as the tab is wide, so size the boxes from it
            tableDedication.SizeChanged += (sender, e) => FitGroupToContents(groupDedication);
            tableDiscordIntro.SizeChanged += (sender, e) => FitGroupToContents(groupDiscord);
            tableDiscord.SizeChanged += (sender, e) => FitGroupToContents(groupDiscord);
            flowSupport.SizeChanged += (sender, e) => FitGroupToContents(groupBoxSupport);
            Shown += (sender, e) => { FitGroupToContents(groupDedication); FitGroupToContents(groupDiscord); FitGroupToContents(groupBoxSupport); };
            FitGroupToContents(groupDedication);
            FitGroupToContents(groupDiscord);
            FitGroupToContents(groupBoxSupport);

            _gameManager = new AowGameManager(AppDataHelper.CheckEmail.FullName, _wrapperConfig.GamesConfig);
            _gameManager.InstallHint = ActivityInstallHint;
            gamesConfig.GameManager = _gameManager;
            activityListView.GameManager = _gameManager;
            if (_gameManager.NeedsDeepScan)
            {
                DeepScanGames();
            }

            if (!_gameManager.CheckWriteAccess())
            {
                OfferPermissionFix();
            }

            lblVersion.Text = string.Format(lblVersion.Text, UpdateHelper.CurrentBuild.DisplayVersion);

            _baseIcon = notifyIcon.Icon;

            SetIcon(IconState.Normal);

            LoadActivityLog();

            LoadConfig();

            cmdQuickStart.Click += (sender, e) => OpenDocument(DocsHelper.QuickStartFile);
            cmdManual.Click += (sender, e) => OpenDocument(DocsHelper.ManualFile);
            cmdLogFolder.Click += new EventHandler(cmdLogFolder_Click);
            cmdCheckUpdates.Click += new EventHandler(cmdCheckUpdates_Click);
            cmdReportBug.Click += new EventHandler(cmdReportBug_Click);
            linkDiscordZig.LinkClicked += new LinkLabelLinkClickedEventHandler(linkDiscord_LinkClicked);
            linkDiscordAow1.LinkClicked += new LinkLabelLinkClickedEventHandler(linkDiscord_LinkClicked);
            linkDiscordAowx.LinkClicked += new LinkLabelLinkClickedEventHandler(linkDiscord_LinkClicked);
            linkDiscordAow2.LinkClicked += new LinkLabelLinkClickedEventHandler(linkDiscord_LinkClicked);
            notifyIcon.BalloonTipClicked += new EventHandler(notifyIcon_BalloonTipClicked);
            notifyIcon.BalloonTipClosed += new EventHandler(notifyIcon_BalloonTipClosed);

            CreateContextMenu();

            BindCustomEvents();

            CheckNotifyIconState(true);

            this.FormClosing += new FormClosingEventHandler(Main_FormClosing);

            ScheduleUpdateCheck();

            Splash.CloseForm();
        }

        #region Updates

        private bool AutoInstallUpdates
        {
            get { return _wrapperConfig != null && _wrapperConfig.PreferencesConfig != null && _wrapperConfig.PreferencesConfig.AutoInstallUpdates; }
        }

        /// <summary>
        /// Every start looks for a newer build on GitHub. With automatic installs on it happens
        /// almost at once, so the update goes in before any turn is in flight.
        /// </summary>
        private void ScheduleUpdateCheck()
        {
            if (!UpdateHelper.IsConfigured)
            {
                cmdCheckUpdates.Visible = false;
                return;
            }

            _updateTimer = new System.Windows.Forms.Timer();
            _updateTimer.Interval = AutoInstallUpdates ? UpdateInstallDelayMilliseconds : UpdateCheckDelayMilliseconds;
            _updateTimer.Tick += new EventHandler(updateTimer_Tick);
            _updateTimer.Start();
        }

        private void updateTimer_Tick(object sender, EventArgs e)
        {
            _updateTimer.Stop();
            _updateTimer.Dispose();
            _updateTimer = null;
            CheckForUpdates(false);
        }

        private void cmdCheckUpdates_Click(object sender, EventArgs e)
        {
            if (_availableUpdate != null)
            {
                OfferUpdate();
            }
            else
            {
                CheckForUpdates(true);
            }
        }

        /// <summary>
        /// Asks GitHub for the newest build. Interactive checks (the About tab link) report every
        /// outcome in a message box. The start-up check installs a newer build straight away when
        /// the preference allows it; otherwise it shows a balloon once per build and leaves the
        /// About tab link saying that an update is available.
        /// </summary>
        private async void CheckForUpdates(bool interactive)
        {
            if (_updateCheckRunning)
            {
                return;
            }
            _updateCheckRunning = true;
            cmdCheckUpdates.Enabled = false;
            cmdCheckUpdates.Text = Translator.Translate(LinkUpdateCheckingKey);

            try
            {
                UpdateInfo update = await UpdateHelper.CheckAsync(CancellationToken.None);
                _availableUpdate = update;

                if (update != null)
                {
                    cmdCheckUpdates.Text = Translator.Translate(LinkUpdateAvailableKey, update.Describe());

                    if (interactive)
                    {
                        OfferUpdate();
                    }
                    else if (AutoInstallUpdates && OkayToShutDown && await InstallSilently(update))
                    {
                        return;
                    }
                    else if (!string.Equals(UpdateHelper.LastNotifiedTag, update.Tag, StringComparison.Ordinal))
                    {
                        UpdateHelper.LastNotifiedTag = update.Tag;
                        _updateBalloonShown = true;
                        notifyIcon.ShowBalloonTip(20000, Translator.Translate(WrapperUpdateAvailableKey), Translator.Translate(WrapperUpdateBalloonKey, update.Describe()), ToolTipIcon.Info);
                    }
                }
                else
                {
                    cmdCheckUpdates.Text = Translator.Translate(cmdCheckUpdates.Name);

                    if (interactive)
                    {
                        MessageBox.Show(this, Translator.Translate(WrapperUpdateNoneKey, UpdateHelper.CurrentBuild.DisplayVersion), Translator.Translate(this.Name), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Update check failed: {0}", ex);
                cmdCheckUpdates.Text = Translator.Translate(cmdCheckUpdates.Name);

                if (interactive)
                {
                    MessageBox.Show(this, Translator.Translate(WrapperUpdateFailedKey, ex.Message), Translator.Translate(this.Name), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                cmdCheckUpdates.Enabled = true;
                _updateCheckRunning = false;
            }
        }

        /// <summary>
        /// Downloads the build without any dialog, tells the player with a balloon and closes the
        /// Wrapper so Program.Main can run the installer, which starts the Wrapper again.
        /// Returns false when the download failed, in which case the caller falls back to
        /// announcing the build.
        /// </summary>
        private async Task<bool> InstallSilently(UpdateInfo update)
        {
            string installer;
            try
            {
                installer = await UpdateHelper.DownloadAsync(update, null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Automatic update download failed, leaving it to the player: {0}", ex);
                return false;
            }

            Trace.TraceInformation("Installing {0} automatically", update.Describe());
            notifyIcon.ShowBalloonTip(10000, Translator.Translate(WrapperUpdateAvailableKey), Translator.Translate(WrapperUpdateInstallingKey, update.Describe()), ToolTipIcon.Info);

            UpdateHelper.PendingInstaller = installer;
            _closeCancel = false;
            this.Close();
            return true;
        }

        /// <summary>
        /// Describes the available build and, if the player agrees, downloads its installer, closes
        /// the Wrapper and leaves the installer for Program.Main to start.
        /// </summary>
        private void OfferUpdate()
        {
            UpdateInfo update = _availableUpdate;
            if (update == null)
            {
                return;
            }

            Maximize();

            string message = Translator.Translate(WrapperUpdateOfferKey,
                update.Describe(),
                update.PublishedAt.ToLocalTime().ToString("g"),
                UpdateHelper.CurrentBuild.DisplayVersion);

            if (!string.IsNullOrWhiteSpace(update.Notes))
            {
                string notes = update.Notes.Trim();
                if (notes.Length > UpdateNotesMaxLength)
                {
                    notes = notes.Substring(0, UpdateNotesMaxLength) + "...";
                }
                message = string.Concat(message, Environment.NewLine, Environment.NewLine, notes);
            }

            if (MessageBox.Show(this, message, Translator.Translate(WrapperUpdateAvailableKey), MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
            {
                return;
            }

            string installer = UpdateForm.Download(this, update);
            if (string.IsNullOrEmpty(installer))
            {
                return;
            }

            UpdateHelper.PendingInstaller = installer;
            _closeCancel = false;
            this.Close();
        }

        private void notifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            if (_updateBalloonShown)
            {
                _updateBalloonShown = false;
                OfferUpdate();
            }
        }

        private void notifyIcon_BalloonTipClosed(object sender, EventArgs e)
        {
            _updateBalloonShown = false;
        }

        #endregion

        #region Form Events

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_closeCancel && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
            }
            else
            {
                e.Cancel = false;

                if (_theServer != null && _theServer.IsRunning)
                {
                    StopServer();
                }

                StopAllPolling();

                if (_aow1GameWatcher != null)
                {
                    _aow1GameWatcher.Stop();
                }
                if (_aow2GameWatcher != null)
                {
                    _aow2GameWatcher.Stop();
                }
                if (_aowSmGameWatcher != null)
                {
                    _aowSmGameWatcher.Stop();
                }

                SetIcon(IconState.None);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Minimized();
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Maximize();
        }

        private void cmdSave_Click(object sender, EventArgs e)
        {
            SaveConfig(true);
        }

        #endregion

        #region Load / Save Config

        private void LoadTranslations()
        {
            if (_wrapperConfig != null &&
                _wrapperConfig.PreferencesConfig != null)
            {
                //This will make non supported regional settings default to English language
                string loadedLanguageCode = Translator.SetLanguage(_wrapperConfig.PreferencesConfig.LanguageCode, DataManagerHelper.LoadLanguages());
                _wrapperConfig.PreferencesConfig.LanguageCode = loadedLanguageCode;
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (!_isNewConfig)
                {
                    this.WindowState = FormWindowState.Minimized;
                    Minimized();
                }

                if (_wrapperConfig != null)
                {
                    if (_wrapperConfig.PreferencesConfig != null)
                    {
                        preferencesConfig.Config = _wrapperConfig.PreferencesConfig;
                    }

                    gamesConfig.Config = _wrapperConfig.GamesConfig;

                    if (_wrapperConfig.AccountsList != null)
                    {
                        accountsConfig.Config = _wrapperConfig.AccountsList;
                        ApplyAccounts(); //This will turn on Config Change Tracking
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                Trace.Flush();
                ShowException(ex);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_isNewConfig &&
                _wrapperConfig != null &&
                _wrapperConfig.AccountsList != null &&
                _wrapperConfig.AccountsList.Accounts != null &&
                _wrapperConfig.AccountsList.Accounts.Count.Equals(0))
            {
                _isNewConfig = false;
                accountsConfig.Add();
            }
        }

        private void SaveConfig(bool reActivate)
        {
            try
            {
                //Accounts
                AccountConfigValuesList accountConfigValuesList = accountsConfig.Config;
                if (accountConfigValuesList != null)
                {
                    _wrapperConfig.AccountsList = accountConfigValuesList;
                    CreateAccountMenu(_menuAccounts, _wrapperConfig.AccountsList);

                    AccountConfigValues primary = accountConfigValuesList.PrimaryAccount;
                    if (primary != null && primary.PollingConfig != null)
                    {
                        panelLocalMessageStore.Visible = primary.PollingConfig.EmailType.Equals(EmailType.POP3);
                    }
                }

                //Preferences
                PreferencesConfigValues preferencesConfigValues = preferencesConfig.Config;
                if (preferencesConfigValues != null)
                {
                    string keyName = Translator.Translate(this.Name);
                    if (preferencesConfigValues.Autostart)
                    {
                        string keyValue = string.Format(WrapperAutostartTemplate, Application.ExecutablePath, ConfigHelper.AUTOSTART_CMD_PARAM);
                        RegistryHelper.SetValue(Registry.CurrentUser, WINDOWS_REG_STARTUP_LOCATION, keyName, keyValue);
                    }
                    else
                    {
                        RegistryHelper.DeleteValue(Registry.CurrentUser, WINDOWS_REG_STARTUP_LOCATION, keyName);
                    }
                }

                //Games: labels, defaults and folders added by hand
                GamesConfigValues gamesConfigValues = gamesConfig.Config;
                if (gamesConfigValues != null)
                {
                    _gameManager.Reload(gamesConfigValues);
                    _wrapperConfig.GamesConfig = _gameManager.ToConfig();
                    gamesConfig.Config = _wrapperConfig.GamesConfig;
                    CreateContextMenu();
                }

                _wrapperConfig.PreferencesConfig = preferencesConfigValues;

                DataManagerHelper.SaveConfig(_wrapperConfig);
                ConfigNeedsSave = false;

                bool activateSuccess = false;

                if (reActivate)
                {
                    activateSuccess = ApplyAccounts();
                }

                if (preferencesConfigValues != null &&
                    !preferencesConfigValues.LanguageCode.Equals(Translator.CurrentLanguageCode) &&
                    activateSuccess)
                {
                    if (MessageBox.Show(Translator.Translate(WrapperRestartRequiredKey), Translator.Translate(this.Name), MessageBoxButtons.YesNo, MessageBoxIcon.Information).Equals(DialogResult.Yes))
                    {
                        RestartWrapper();
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                Trace.Flush();
                ShowException(ex);
            }
        }

        #endregion

        #region Custom Events

        private void RestartWrapper()
        {
            string thisProcessId = Process.GetCurrentProcess().Id.ToString();
            _closeCancel = false;
            this.Close();
            Process.Start(Application.ExecutablePath, string.Format(WrapperRestartTemplate, ConfigHelper.RESTART_CMD_PARAM, thisProcessId));
        }

        private void BindCustomEvents()
        {
            _shutDownEvent += new EventHandler(ShutDown);
            _maximizeEvent += new EventHandler(Maximize);
            _activityLogRefresh += new EventHandler(ActivityLogRefresh);
            _gameManager.OnGameSaved += new AowGameSavedEventHandler(OnAowGameSaved);

            accountsConfig.Account_Activated += new AccountActivatedEventHandler(Account_Activated);
            accountsConfig.Config_Changed += new EventHandler(Rebuild_Account_Menu);

            activityListView.OnDoubleClick += new ActivityListViewEventHandler(ActivityListViewDoubleClicked);
            activityListView.OnListChanged += new EventHandler(ActivityLogChanged);
            activityListView.OnDeleteClick += new ActivityListViewEventHandler(ActivityListViewGamesDeleted);
            activityListView.OnMarkAsEnded += new ActivityListViewEventHandler(ActivityListViewGamesMarkedAsEnded);
            activityListView.OnResendClick += new ActivityListViewEventHandler(ActivityListViewResend);
            activityListView.OnMoveTo += new ActivityMoveEventHandler(ActivityListViewMoveTo);
        }

        private void ShutDown(object sender, EventArgs e)
        {
            if (OkayToShutDown)
            {
                _closeCancel = false;
                this.Close();
            }
        }

        private void StartedGameWatchCompleted(object sender, AowGameType gameType)
        {
            switch (gameType)
            {
                case AowGameType.Aow1:
                    _aow1GameWatcher = null;
                    break;
                case AowGameType.Aow2:
                    _aow2GameWatcher = null;
                    break;
                case AowGameType.AowSm:
                case AowGameType.AowMpe:
                    _aowSmGameWatcher = null;
                    break;
            }
        }

        private void OnConfigNeedsSave(object sender, EventArgs e)
        {
            ConfigNeedsSave = true;
        }

        #endregion

        #region Private Utility Methods

        private void PlaySound(string theFile)
        {
            if (!string.IsNullOrEmpty(theFile))
            {
                System.Media.SoundPlayer myPlayer = new System.Media.SoundPlayer();
                string file = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), theFile);
                if (System.IO.File.Exists(file))
                {
                    myPlayer.SoundLocation = file;
                    myPlayer.Play();
                    myPlayer.Dispose();
                    myPlayer = null;
                }
            }
        }

        private void CheckNotifyIconState()
        {
            CheckNotifyIconState(false);
        }

        private void CheckNotifyIconState(bool showBaloon)
        {
            IconState theState = IconState.Normal;

            if (IsAnySending)
            {
                SetIcon(IconState.Sending);
            }
            else if (IsAnyPolling)
            {
                SetIcon(IconState.Checking);
            }
            else
            {
                int fileCount = _activityLog.GetUnSentActivitiesCount();
                if (fileCount > 0)
                {
                    theState = IconState.EmailWaiting;

                    if (showBaloon)
                    {
                        this.Activate();
                        notifyIcon.ShowBalloonTip(5000, Translator.Translate(this.Name), Translator.Translate(WrapperGamesWaitingKey, fileCount.ToString()), ToolTipIcon.Info);
                    }
                }

                SetIcon(theState);
            }
        }

        private void RaiseEvent(EventHandler theDelegate, object sender, EventArgs e)
        {
            if (theDelegate != null)
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(theDelegate, sender, e);
                }
                else
                {
                    theDelegate(sender, e);
                }
            }
        }

        /// <summary>Runs window code from any thread; the local mail server calls this from its own thread.</summary>
        private void RunOnUiThread(Action action)
        {
            if (this.InvokeRequired)
            {
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke(action);
                }
            }
            else
            {
                action();
            }
        }

        private void SetIcon(IconState theState)
        {
            Icon theIcon = null;
            string status = null;

            switch (theState)
            {
                case IconState.Normal:
                    theIcon = _baseIcon;
                    status = null;
                    break;
                case IconState.Sending:
                    theIcon = GetIcon(theState.ToString());
                    status = Translator.TranslateEnum(theState);
                    break;
                case IconState.Checking:
                    theIcon = GetIcon(theState.ToString());
                    status = Translator.TranslateEnum(theState);
                    break;
                case IconState.EmailWaiting:
                    theIcon = GetIcon(theState.ToString());
                    status = Translator.TranslateEnum(theState);
                    break;
                default:
                    status = null;
                    theIcon = null;
                    break;
            }
            string defaultText = Translator.Translate(this.Name);
            notifyIcon.Text = !string.IsNullOrEmpty(status) ? string.Format("{0}: {1}", defaultText, status) : defaultText;
            notifyIcon.Icon = theIcon;
        }

        private Icon GetIcon(string theKey)
        {
            Icon returnVal = null;

            if (imageListIcons.Images.ContainsKey(theKey))
            {
                //System.Drawing.Bitmap.GetHicon() can throw a System.Runtime.InteropServices.ExternalException: A generic error occurred in GDI+.
                //returnVal = Icon.FromHandle(((Bitmap)imageListIcons.Images[theKey]).GetHicon());

                //FIX
                returnVal = FlimFlan.IconEncoder.Converter.BitmapToIcon(imageListIcons.Images[theKey] as Bitmap);
            }

            return returnVal;
        }

        private void Minimized()
        {
            this.SuspendLayout();

            if (this.WindowState == FormWindowState.Minimized)
            {
                this.ShowInTaskbar = false;
                this.Visible = false;
            }

            this.ResumeLayout();
        }

        private void Maximize(object sender, EventArgs e)
        {
            Maximize();
        }

        private void Maximize()
        {
            this.SuspendLayout();

            if (this.WindowState == FormWindowState.Minimized)
            {
                if (_activityLog != null && 
                    _activityLog.Activities != null && 
                    _activityLog.Activities.Count > 0)
                {
                    tabControlMain.SelectedTab = tabControlMain.TabPages["tabActivity"];
                }
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                this.Visible = true;
            }
            
            this.Activate();
            this.ResumeLayout();
        }

        private void OpenDocument(string fileName)
        {
            try
            {
                if (!DocsHelper.Open(fileName))
                {
                    MessageBox.Show(this, Translator.Translate(MsgDocumentMissingKey, fileName), Translator.Translate(this.Name), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Could not open {0}: {1}", fileName, ex);
                MessageBox.Show(this, ex.Message, Translator.Translate(this.Name), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void cmdLogFolder_Click(object sender, EventArgs e)
        {
            LogHelper.OpenLogFolder();
        }

        /// <summary>
        /// Makes a group box exactly tall enough for its top-docked contents. AutoSize cannot do this
        /// for wrapped text, because it measures the contents before they are narrowed to the box.
        /// </summary>
        private static void FitGroupToContents(GroupBox group)
        {
            //Not filtered on Visible: that is false for everything until the form is shown
            int contents = group.Controls.Cast<Control>().Sum(control => control.Height);
            int frame = group.Height - group.DisplayRectangle.Height;
            group.Height = contents + frame;
        }

        /// <summary>The Discord invites on the About tab keep their address in the link's Tag.</summary>
        private void linkDiscord_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LinkLabel link = sender as LinkLabel;
            string url = link != null ? link.Tag as string : null;
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            try
            {
                link.LinkVisited = true;
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Trace.TraceError("Could not open {0}: {1}", url, ex);
            }
        }

        private void cmdReportBug_Click(object sender, EventArgs e)
        {
            BugReportForm.Show(this, BugReportHelper.FindSender(_wrapperConfig));
        }

        private void ShowException(Exception ex)
        {
            this.Invoke(new EventHandler(this.Maximize));

            ShowExceptionDialog(ex, Translator.Translate(this.Name), MessageBoxIcon.Error, Translator.Translate(ButtonKeyOK));
        }

        /// <summary>
        /// Shows the exception on the UI thread and returns the index of the button clicked.
        /// </summary>
        private int ShowExceptionDialog(Exception ex, string caption, MessageBoxIcon icon, params string[] buttons)
        {
            _showingExceptionCount++;
            try
            {
                if (this.InvokeRequired)
                {
                    return (int)this.Invoke(new Func<int>(() => ExceptionDialog.Show(this, caption, ex, icon, buttons)));
                }
                return ExceptionDialog.Show(this, caption, ex, icon, buttons);
            }
            finally
            {
                _showingExceptionCount--;
            }
        }

        private void StartGame(AowGame theGame)
        {
            switch (theGame.GameType)
            {
                case AowGameType.Aow1:
                    if (_aow1GameWatcher == null)
                    {
                        _aow1GameWatcher = new StartedTaskWatcher(theGame, new StartedTaskCompleteEventHandler(StartedGameWatchCompleted));
                        _aow1GameWatcher.Start();
                    }
                    break;
                case AowGameType.Aow2:
                    if (_aow2GameWatcher == null)
                    {
                        _aow2GameWatcher = new StartedTaskWatcher(theGame, new StartedTaskCompleteEventHandler(StartedGameWatchCompleted));
                        _aow2GameWatcher.Start();
                    }
                    break;
                case AowGameType.AowSm:
                case AowGameType.AowMpe:
                    if (_aowSmGameWatcher == null)
                    {
                        _aowSmGameWatcher = new StartedTaskWatcher(theGame, new StartedTaskCompleteEventHandler(StartedGameWatchCompleted));
                        _aowSmGameWatcher.Start();
                    }
                    break;
            }
        }

        #endregion

        #region Incoming Email

        #region Pollers and senders, one per account

        private bool IsAnyPolling
        {
            get { return _pollers.Values.Any(poller => poller.IsPolling); }
        }

        private bool IsAnySending
        {
            get { return _senders.Values.Any(sender => sender.IsSending); }
        }

        /// <summary>Starts a watcher for every account that checks for email and has sign-in details.</summary>
        private void StartAllPolling()
        {
            StopAllPolling();

            if (_wrapperConfig == null || _wrapperConfig.AccountsList == null || _wrapperConfig.AccountsList.Accounts == null)
            {
                return;
            }

            HashSet<string> mailboxes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (AccountConfigValues account in _wrapperConfig.AccountsList.ActiveAccounts)
            {
                PollingConfigValues polling = account.PollingConfig;
                bool hasCredentials = !string.IsNullOrEmpty(polling.Username) && !string.IsNullOrEmpty(polling.Server) &&
                    (!string.IsNullOrEmpty(polling.Password) || MicrosoftOAuth.IsProvider(account.OAuthProvider));

                //Two accounts on the same mailbox would download every turn twice
                if (!hasCredentials || !mailboxes.Add(polling.Username + "@" + polling.Server))
                {
                    continue;
                }

                BasePoller poller = CreatePoller(account, _wrapperConfig.PreferencesConfig);
                _pollers[account.Name] = poller;
                poller.Start();
            }
        }

        private BasePoller CreatePoller(AccountConfigValues account, PreferencesConfigValues preferences)
        {
            PollingConfigValues polling = account.PollingConfig;
            EmailSaveFolder saveFolder = preferences != null ? preferences.SaveFolder : EmailSaveFolder.EmailIn;
            BasePoller poller;

            if (polling.EmailType == EmailType.POP3)
            {
                poller = new Pop3Poller(polling.Server, polling.Port, polling.SSLType, polling.Username, polling.PasswordTrue, polling.PollInterval, saveFolder, _gameManager);
            }
            else
            {
                poller = new ImapPoller(polling.Server, polling.Port, polling.SSLType, polling.Username, polling.PasswordTrue, polling.PollInterval, saveFolder, _gameManager);
            }

            poller.AccountName = account.Name;
            poller.OAuthProvider = account.OAuthProvider;
            poller.OnEmailEvent += new PollerEmailEventHandler(PollerEmailEvent);
            return poller;
        }

        private void StopAllPolling()
        {
            foreach (BasePoller poller in _pollers.Values)
            {
                poller.Stop();
            }
            _pollers.Clear();
        }

        private void PollAll()
        {
            foreach (BasePoller poller in _pollers.Values)
            {
                poller.PollNow();
            }
        }

        /// <summary>Builds a sender for every account; games received on an account reply through that account.</summary>
        private void CreateAllSenders()
        {
            if (IsAnySending)
            {
                return;
            }

            foreach (SmtpSender sender in _senders.Values)
            {
                sender.Dispose();
            }
            _senders.Clear();

            if (_wrapperConfig == null || _wrapperConfig.AccountsList == null || _wrapperConfig.AccountsList.Accounts == null)
            {
                return;
            }

            foreach (AccountConfigValues account in _wrapperConfig.AccountsList.Accounts)
            {
                if (account.SmtpConfig != null && !string.IsNullOrEmpty(account.SmtpConfig.SmtpServer) && !string.IsNullOrEmpty(account.Name))
                {
                    _senders[account.Name] = CreateSender(account);
                }
            }
        }

        private SmtpSender CreateSender(AccountConfigValues account)
        {
            SmtpConfigValues smtp = account.SmtpConfig;
            PollingConfigValues polling = account.PollingConfig ?? new PollingConfigValues();
            SmtpSender sender;

            if (smtp.Authentication)
            {
                sender = new SmtpSender(
                    smtp.SmtpServer,
                    smtp.Port,
                    smtp.UsePollingCredentials ? polling.Username : smtp.Username,
                    smtp.UsePollingCredentials ? polling.PasswordTrue : smtp.PasswordTrue,
                    smtp.SmtpSSLType,
                    smtp.BCCMyself);
            }
            else
            {
                sender = new SmtpSender(smtp.SmtpServer, smtp.Port, smtp.SmtpSSLType, smtp.BCCMyself);
            }

            sender.AccountName = account.Name;
            sender.OAuthProvider = account.OAuthProvider;
            sender.OnEmailSent += new SmtpSenderSentEventHandler(SmtpSenderSent);
            return sender;
        }

        /// <summary>The sender for an account, falling back to the primary account and then to any account.</summary>
        private SmtpSender GetSender(string accountName)
        {
            SmtpSender sender;
            if (!string.IsNullOrEmpty(accountName) && _senders.TryGetValue(accountName, out sender))
            {
                return sender;
            }

            AccountConfigValues primary = (_wrapperConfig != null && _wrapperConfig.AccountsList != null) ? _wrapperConfig.AccountsList.PrimaryAccount : null;
            if (primary != null && !string.IsNullOrEmpty(primary.Name) && _senders.TryGetValue(primary.Name, out sender))
            {
                return sender;
            }

            return _senders.Values.FirstOrDefault();
        }

        private AccountConfigValues AccountOf(SmtpSender sender)
        {
            return (sender != null && _wrapperConfig != null && _wrapperConfig.AccountsList != null)
                ? _wrapperConfig.AccountsList.GetAccountByName(sender.AccountName)
                : null;
        }

        /// <summary>
        /// Picks the account a turn goes out through (the one the game arrived on, otherwise the primary
        /// account) and stamps that account's address on the message so the reply comes from the right place.
        /// </summary>
        private SmtpSender RouteOutgoing(MimeMessage theEmail)
        {
            string accountName = null;

            MimePart attachment = MailHelper.GetFirstAttachment(theEmail);
            if (attachment != null)
            {
                Activity activity = _activityLog.GetLastActivityByFileName(attachment.FileName);
                if (activity != null)
                {
                    accountName = activity.AccountName;
                }
            }

            SmtpSender sender = GetSender(accountName);
            AccountConfigValues account = AccountOf(sender);

            if (account != null && account.SmtpConfig != null && !string.IsNullOrEmpty(account.SmtpConfig.EmailAddress))
            {
                theEmail.From.Clear();
                theEmail.From.Add(new MailboxAddress(string.Empty, account.SmtpConfig.EmailAddress));
                theEmail.Sender = null;
            }

            return sender;
        }

        #endregion

        private void PollerEmailEvent(object sender, PollerEventArgs e)
        {
            if (this.InvokeRequired)
            {
                //Raised on the poller thread; everything below touches the window
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke(new PollerEmailEventHandler(PollerEmailEvent), sender, e);
                }
                return;
            }

            if (_closeCancel)
            {
                switch (e.PollState)
                {
                    case PollState.Begin:
                        SetIcon(IconState.Checking);
                        break;
                    case PollState.Aborted:
                        CheckNotifyIconState();
                        break;
                    case PollState.End:
                        if (e.EmailRecieved)
                        {
                            if (_wrapperConfig.PreferencesConfig != null && _wrapperConfig.PreferencesConfig.PlaySoundOnEmail)
                            {
                                PlaySound(ConfigHelper.NotifySound);
                            }
                            RaiseEvent(_activityLogRefresh, this, new EventArgs());
                        }
                        if (e.Exception is MailKit.Security.AuthenticationException)
                        {
                            //Retrying a rejected password every few minutes only annoys the user (and the provider).
                            //Activating or saving the account starts polling again.
                            BasePoller failed = sender as BasePoller;
                            if (failed != null)
                            {
                                failed.Stop();
                                _pollers.Remove(failed.AccountName ?? string.Empty);
                            }
                            notifyIcon.ShowBalloonTip(20000, Translator.Translate(WrapperPollFailedKey), BuildPollAuthFailedMessage(failed), ToolTipIcon.Warning);
                        }
                        else if (e.Exception != null)
                        {
                            notifyIcon.ShowBalloonTip(15000, Translator.Translate(WrapperPollFailedKey), e.Exception.Message, ToolTipIcon.Error);
                        }
                        else
                        {
                            //The connection should be good
                            RetrySendFailures();
                        }
                        CheckNotifyIconState();
                        break;
                }
            }
        }

        private string BuildPollAuthFailedMessage(BasePoller poller)
        {
            string server = poller != null ? (poller.Host ?? string.Empty) : string.Empty;
            string user = poller != null ? (poller.Username ?? string.Empty) : string.Empty;

            string message = Translator.Translate(WrapperPollAuthFailedKey, server, user);

            ProviderHint hint = ProviderHints.ForHost(server);
            if (hint != null)
            {
                message = Translator.Translate(hint.ShortMessageKey) + " " + message;
            }

            return message;
        }

        private void cmdMessageStore_Click(object sender, EventArgs e)
        {
            if (_wrapperConfig != null)
            {
                AccountConfigValues theAccount = _wrapperConfig.AccountsList.PrimaryAccount;

                if (theAccount != null &&
                    !string.IsNullOrEmpty(theAccount.PollingConfig.Username) &&
                    !string.IsNullOrEmpty(theAccount.PollingConfig.Server))
                {
                    MessageStore form = new MessageStore(theAccount.PollingConfig.Username, theAccount.PollingConfig.Server);

                    if (form.ShowDialog(this).Equals(DialogResult.OK))
                    {
                        if (_pollers.Count > 0)
                        {
                            PollAll();
                        }
                        else
                        {
                            StartAllPolling();
                        }
                    }
                }
            }
        }

        #endregion

        #region Outgoing Email

        private void StartServer(int thePort)
        {
            try
            {
                _theServer = new SimpleServer(thePort, ProcessSMTPRequest);
                new Thread(new ThreadStart(_theServer.Start)).Start();
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                Trace.Flush();
                ShowException(ex);
            }
        }

        private void StopServer()
        {
            if (_theServer != null)
            {
                _theServer.Stop();
                _theServer = null;
            }
        }

        public void ProcessSMTPRequest(Socket socket)
        {
            SMTPProcessor theSmtpProcessor = null;

            try
            {
                SmtpSpool smtpSpooler = new SmtpSpool();

                theSmtpProcessor = new SMTPProcessor(string.Concat(Environment.MachineName, ".com"), new AnyRecipientFilter(), smtpSpooler);

                RunOnUiThread(() => SetIcon(IconState.Sending));

                theSmtpProcessor.ProcessConnection(socket);

                MimeMessage theEmail = smtpSpooler.SpooledEmail;

                if (theEmail != null)
                {
                    TagOutgoingInstall(theEmail);

                    SmtpSender sender = RouteOutgoing(theEmail);
                    if (sender == null)
                    {
                        throw new InvalidOperationException(Translator.Translate(WrapperNoSendAccountKey));
                    }
                    ResendHelper.Save(theEmail);
                    sender.SendMessage(theEmail);
                }

                theSmtpProcessor.Dispose();
                theSmtpProcessor = null;

                RunOnUiThread(() => CheckNotifyIconState());
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                Trace.Flush();
                ShowException(ex);
            }
            finally
            {
                theSmtpProcessor = null;
            }
        }

        private void SmtpSenderSent(object sender, SmtpSendResponse theResponse)
        {
            if (this.InvokeRequired)
            {
                //Raised on the sender thread; everything below touches the window
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke(new SmtpSenderSentEventHandler(SmtpSenderSent), sender, theResponse);
                }
                return;
            }

            if (_closeCancel)
            {
                this.Activate();

                SmtpSender smtpSender = sender as SmtpSender;
                if (theResponse.IsSuccess)
                {
                    SmtpSendSuccess(smtpSender, theResponse);
                }
                else
                {
                    SmtpSendFailure(smtpSender, theResponse);
                }

                CheckNotifyIconState();
            }
        }

        private void SmtpSendSuccess(SmtpSender smtpSender, SmtpSendResponse theResponse)
        {
            if (theResponse.IsSuccess)
            {
                AccountConfigValues account = AccountOf(smtpSender);
                if (account != null && account.SmtpConfig != null && !account.SmtpConfig.Verified)
                {
                    account.SmtpConfig.Verified = true;
                    DataManagerHelper.SaveConfig(_wrapperConfig);
                }

                notifyIcon.ShowBalloonTip(15000, theResponse.GameEmail.Subject, Translator.Translate(WrapperEmailSentSuccessKey, MailHelper.GetFirstToAddress(theResponse.GameEmail)), ToolTipIcon.Info);
                if (_wrapperConfig.PreferencesConfig != null && _wrapperConfig.PreferencesConfig.PlaySoundOnSend)
                {
                    PlaySound(ConfigHelper.SentSound);
                }

                MimePart theAttachment = MailHelper.GetFirstAttachment(theResponse.GameEmail);
                if (theAttachment != null)
                {
                    Activity theActivity = UpdateActivitySent(theAttachment, smtpSender != null ? smtpSender.AccountName : null);
                    RecordOutgoingInstall(theResponse.GameEmail, theActivity);

                    if (_wrapperConfig.PreferencesConfig != null && _wrapperConfig.PreferencesConfig.CopyToEmailOut)
                    {
                        try
                        {
                            _gameManager.CopyToEmailOut(theAttachment, _gameManager.GetGameForActivity(theActivity));
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceError(ex.ToString());
                            Trace.Flush();
                            ShowException(ex);
                        }
                    }
                }

                theResponse.Dispose();
            }
        }

        private void SmtpSendFailure(SmtpSender smtpSender, SmtpSendResponse theResponse)
        {
            if (!theResponse.IsSuccess)
            {
                MimePart theAttachment = MailHelper.GetFirstAttachment(theResponse.GameEmail);
                if (theAttachment != null)
                {
                    UpdateActivitySendError(theAttachment);
                }

                AccountConfigValues account = AccountOf(smtpSender);
                if (account != null && account.SmtpConfig != null && account.SmtpConfig.Verified)
                {
                    //Just show Baloon error
                    notifyIcon.ShowBalloonTip(15000, theResponse.GameEmail.Subject, theResponse.Exception.Message, ToolTipIcon.Error);
                    theResponse.Dispose();
                }
                else
                {
                    //Show full Message Box error
                    ShowSmtpExceptionMessageBox(smtpSender, theResponse);
                }
            }
        }

        private void ShowSmtpExceptionMessageBox(SmtpSender smtpSender, SmtpSendResponse theResponse)
        {
            RaiseEvent(_maximizeEvent, this, new EventArgs());

            string errorMessage = Translator.Translate(WrapperEmailSentFailedKey, theResponse.GameEmail.Subject, MailHelper.GetFirstToAddress(theResponse.GameEmail));

            AccountConfigValues account = AccountOf(smtpSender);
            if (theResponse.Exception is MailKit.Security.AuthenticationException &&
                account != null && account.SmtpConfig != null)
            {
                ProviderHint hint = ProviderHints.ForHost(account.SmtpConfig.SmtpServer);
                if (hint != null)
                {
                    errorMessage += Environment.NewLine + Environment.NewLine + Translator.Translate(hint.MessageKey);
                }
            }
            ApplicationException showException = new ApplicationException(errorMessage, theResponse.Exception);

            int clicked = ShowExceptionDialog(showException, Translator.Translate(this.Name), MessageBoxIcon.Question, Translator.Translate(ButtonKeyResend), Translator.Translate(ButtonKeyCancel));

            if (clicked == 0 && smtpSender != null)
            {
                smtpSender.SendMessage(theResponse.GameEmail);
            }
            else
            {
                theResponse.Dispose();
            }
        }

        private void RetrySendFailures()
        {
            foreach (Activity activity in _activityLog.GetRetryActivities())
            {
                SmtpSender sender = GetSender(activity.AccountName);
                if (sender != null && ResendHelper.CanResend(activity.FileName))
                {
                    MimeMessage email = ResendHelper.Load(activity.FileName);
                    if (email != null)
                    {
                        sender.SendMessage(email);
                    }
                }
            }
        }

        #endregion

        #region Accounts

        private void Account_Activated(object sender, AccountConfigValues theAccount, bool dirty)
        {
            ApplyAccounts();

            if (dirty)
            {
                //We have unsaved data
                SaveConfig(false);
            }
        }

        /// <summary>
        /// Wires everything to the current account list: a watcher per active account, a sender per account,
        /// and the games pointed at the primary account's address.
        /// </summary>
        private bool ApplyAccounts()
        {
            bool success = false;

            AccountConfigValues primary = (_wrapperConfig != null && _wrapperConfig.AccountsList != null) ? _wrapperConfig.AccountsList.PrimaryAccount : null;

            //Don't rewire while a send is in progress
            if (IsAnySending)
            {
                MessageBox.Show(Translator.Translate(WrapperCannotActivateAccountMessageBoxKey, primary != null ? primary.Name : string.Empty), Translator.Translate(this.Name), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                ConfigChangeTracking = false;

                if (primary != null)
                {
                    _wrapperConfig.AccountsList.ActiveAccountName = primary.Name;
                    _wrapperConfig.AccountsList.StartUpAccountName = primary.Name;
                }

                panelLocalMessageStore.Visible = primary != null && primary.PollingConfig != null && primary.PollingConfig.EmailType.Equals(EmailType.POP3);

                //Belt and braces for Game to Wrapper communication
                int listenPort = PreferencesConfigValues.GameWrapperDataPortDefault;

                if (_wrapperConfig.PreferencesConfig != null &&
                    _wrapperConfig.PreferencesConfig.GameWrapperDataPort > 0)
                {
                    listenPort = _wrapperConfig.PreferencesConfig.GameWrapperDataPort;
                }

                if (_theServer == null)
                {
                    //Start Wrapper SMTP Server
                    StartServer(listenPort);
                }
                else if (_theServer != null &&
                    _theServer.IsRunning &&
                    !_theServer.Port.Equals(listenPort))
                {
                    //They changed the port, the SMTP Server needs a restart
                    StopServer();
                    StartServer(listenPort);
                }

                if (primary != null && primary.SmtpConfig != null)
                {
                    _gameManager.SetEmailConfigAll(AppDataHelper.CheckEmail.FullName, primary.SmtpConfig.EmailAddress, string.Format(GameSmtpServerTemplate, _theServer.Port));
                }

                CreateAllSenders();
                StartAllPolling();

                success = true;

                int activeCount = _wrapperConfig.AccountsList != null ? _wrapperConfig.AccountsList.ActiveAccounts.Count : 0;
                if (primary != null && primary.SmtpConfig != null && !string.IsNullOrEmpty(primary.SmtpConfig.EmailAddress))
                {
                    this.Text = activeCount > 1
                        ? string.Format(MainFormTitleMoreTemplate, Translator.Translate(this.Name), primary.SmtpConfig.EmailAddress, activeCount - 1)
                        : string.Format(MainFormTitleTemplate, Translator.Translate(this.Name), primary.SmtpConfig.EmailAddress);
                }
                else
                {
                    this.Text = Translator.Translate(this.Name);
                }

                accountsConfig.Refresh();

                ConfigChangeTracking = true;
            }

            return success;
        }

        #endregion

        #region Activity Log

        //Raised by the AowGameManager class
        private void OnAowGameSaved(object sender, AowGameSavedEventArgs e)
        {
            ResendHelper.Delete(e.FileName); //Avoids the user resending the previous turn by mistake

            Activity lastActivity = _activityLog.GetLastActivityByFileName(e.FileName);

            if (lastActivity != null)
            {
                _activityLog.Activities.Remove(lastActivity);
            }

            Activity newActivity = new Activity(e);
            _activityLog.Activities.Add(newActivity);
        }

        /// <summary>
        /// First start (or nothing remembered yet): walks the drives for copies of the games in the
        /// background, then applies the result, saves it so later starts skip the walk, and refreshes
        /// the Games tab and the tray menu. A tab with unsaved edits is left alone.
        /// </summary>
        private async void DeepScanGames()
        {
            GamesConfigValues known = _gameManager.ToConfig();
            List<AowGame> detected;
            try
            {
                detected = await Task.Run(() => GameDetector.Detect(known.Installs, true));
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Background game scan failed: {0}", ex);
                return;
            }

            if (IsDisposed)
            {
                return;
            }

            _gameManager.Apply(detected, known);
            _wrapperConfig.GamesConfig = _gameManager.ToConfig();
            DataManagerHelper.SaveConfig(_wrapperConfig);

            if (!ConfigNeedsSave)
            {
                gamesConfig.Config = _wrapperConfig.GamesConfig;
            }
            CreateContextMenu();
            CheckNotifyIconState();
        }

        /// <summary>
        /// Some game folders (typically under a Program Files folder) are read-only for ordinary
        /// accounts. Offers to grant the account write access through an elevated icacls run.
        /// </summary>
        private void OfferPermissionFix()
        {
            string title = Translator.Translate(this.Name);
            string message = string.Concat(
                Translator.Translate(WrapperWriteAccessMessageBoxKey, _gameManager.GetEmailInFolderList()),
                Environment.NewLine, Environment.NewLine,
                Translator.Translate(WrapperFixPermissionsKey));

            if (MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                PermissionHelper.GrantWriteAccess(_gameManager.RootsWithoutWriteAccess);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Permission fix failed: {0}", ex);
            }

            _gameManager.ResetWriteAccess();
            if (_gameManager.CheckWriteAccess())
            {
                MessageBox.Show(Translator.Translate(WrapperFixPermissionsDoneKey), title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(Translator.Translate(WrapperFixPermissionsFailedKey, _gameManager.GetEmailInFolderList()), title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Folder the game was last seen in, from the activity log, for the game manager's routing.</summary>
        private string ActivityInstallHint(AowGameType gameType, string fileName)
        {
            Activity last = _activityLog != null ? _activityLog.GetLastActivityByFileName(fileName) : null;
            return last != null && last.GameType.Equals(gameType) ? last.InstallFolder : null;
        }

        /// <summary>
        /// Works out which copy of the game produced the turn and stamps that copy's label on the
        /// email, so the receiving Wrapper can put the turn in its matching copy.
        /// </summary>
        private void TagOutgoingInstall(MimeMessage theEmail)
        {
            try
            {
                MimePart theAttachment = MailHelper.GetFirstAttachment(theEmail);
                if (theAttachment == null)
                {
                    return;
                }

                using (ASGFileInfo theASG = new ASGFileInfo(theAttachment))
                {
                    AowGame install = _gameManager.ResolveOutgoing(theASG.GameType, theASG.FileNameTrue);
                    if (install == null)
                    {
                        return;
                    }

                    MailHelper.SetModLabel(theEmail, install.Label);
                    lock (_outgoingInstalls)
                    {
                        _outgoingInstalls[theEmail.MessageId ?? string.Empty] = install.Folder;
                    }
                    Trace.TraceInformation("Turn {0} sent from {1}", theASG.FileNameTrue, install);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Could not work out which copy of the game sent the turn: {0}", ex);
            }
        }

        /// <summary>Remembers in the activity log which copy a sent turn came from and the label it went out under.</summary>
        private void RecordOutgoingInstall(MimeMessage theEmail, Activity theActivity)
        {
            if (theEmail == null || theActivity == null)
            {
                return;
            }

            string folder = null;
            string messageId = theEmail.MessageId ?? string.Empty;
            lock (_outgoingInstalls)
            {
                if (_outgoingInstalls.TryGetValue(messageId, out folder))
                {
                    _outgoingInstalls.Remove(messageId);
                }
            }

            if (!string.IsNullOrEmpty(folder))
            {
                theActivity.InstallFolder = folder;
            }

            string label = MailHelper.GetModLabel(theEmail);
            if (!string.IsNullOrEmpty(label))
            {
                theActivity.ModLabel = label;
            }
        }

        /// <summary>Turns waiting in one copy of a game; turns with no recorded copy count for the default copy.</summary>
        private int UnsentActivitiesFor(AowGame game)
        {
            return _activityLog.Activities.Count(activity =>
                activity.Status.Equals(ActivityState.Received) &&
                activity.GameType.Equals(game.GameType) &&
                (game.IsFolder(activity.InstallFolder) || (string.IsNullOrEmpty(activity.InstallFolder) && game.IsDefault)));
        }

        private void ActivityListViewMoveTo(object sender, Activity activity, AowGame target)
        {
            try
            {
                _gameManager.MoveGame(activity.GameType, activity.FileName, target);
                activity.InstallFolder = target.Folder;
                DataManagerHelper.SaveActivityLog(_activityLog);
                activityListView.Refresh();
                CheckNotifyIconState();
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                Trace.Flush();
                ShowException(ex);
            }
        }

        private void LoadActivityLog()
        {
            try
            {
                activityListView.SmallImageList = imageListIcons;
                _activityLog = DataManagerHelper.LoadActivityLog();
                activityListView.ActivityLog = _activityLog;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                Trace.Flush();
                ShowException(ex);
            }
        }

        private void ActivityListViewDoubleClicked(object sender, List<Activity> list)
        {
            if (list != null && list.Count > 0)
            {
                AowGame theGame = _gameManager.GetGameForActivity(list[0]);
                if (theGame != null)
                {
                    StartGame(theGame);
                }
            }
        }

        private void ActivityListViewGamesDeleted(object sender, List<Activity> list)
        {
            if (list != null && list.Count > 0)
            {
                try
                {
                    list.ForEach(deletedActivity =>
                    {
                        TurnLogger.DeleteLog(deletedActivity.FileName);
                        ResendHelper.Delete(deletedActivity.FileName);
                        _gameManager.DeleteGame(deletedActivity.GameType, deletedActivity.FileName);
                    });
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                    Trace.Flush();
                    ShowException(ex);
                }
            }
        }

        private void ActivityListViewGamesMarkedAsEnded(object sender, List<Activity> list)
        {
            if (list != null && list.Count > 0)
            {
                try
                {
                    if (MessageBox.Show(Translator.Translate(WrapperArchiveGameMessageBoxKey, ConfigHelper.EndedFolder), Translator.Translate(this.Name), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        list.ForEach(endedActivity => _gameManager.ArchiveEndedGame(endedActivity.GameType, endedActivity.FileName, ConfigHelper.EndedFolder));
                    }

                    list.ForEach(endedActivity =>
                    {
                        TurnLogger.DeleteLog(endedActivity.FileName);
                        ResendHelper.Delete(endedActivity.FileName);
                    });
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                    Trace.Flush();
                    ShowException(ex);
                }
            }
        }

        private void ActivityListViewResend(object sender, List<Activity> list)
        {
            if (list != null && list.Count > 0)
            {
                try
                {
                    foreach (Activity activity in list)
                    {
                        MimeMessage theEmail = ResendHelper.Load(activity.FileName);
                        SmtpSender resendSender = GetSender(activity.AccountName);
                        if (theEmail != null && resendSender != null && theEmail.To.Mailboxes.Any())
                        {
                            string newToAddress = MailHelper.GetFirstToAddress(theEmail);

                            Image gameTypeImage = null;
                            string gameType = activity.GameType.ToString();
                            if (imageListIcons.Images.IndexOfKey(gameType) >= 0)
                            {
                                gameTypeImage = imageListIcons.Images[gameType];
                            }

                            if (InputBox.Show(activity.FileName, Translator.Translate(WrapperResendToKey), ref newToAddress, gameTypeImage).Equals(DialogResult.OK))
                            {
                                if (!newToAddress.Equals(MailHelper.GetFirstToAddress(theEmail), StringComparison.InvariantCultureIgnoreCase))
                                {
                                    theEmail.To.Clear();
                                    theEmail.To.Add(MailboxAddress.Parse(newToAddress));
                                }

                                ResendHelper.Save(theEmail);
                                resendSender.SendMessage(theEmail);

                                CheckNotifyIconState();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                    Trace.Flush();
                    ShowException(ex);
                }
            }
        }

        private Activity UpdateActivitySent(MimePart theAttachment, string accountName)
        {
            Activity theActivity = null;
            theActivity = _activityLog.GetLastActivityByFileName(theAttachment.FileName);

            if (theActivity != null)
            {
                theActivity.Status = ActivityState.Sent;
                if (!string.IsNullOrEmpty(accountName))
                {
                    theActivity.AccountName = accountName;
                }
                if (theActivity.GameType.Equals(AowGameType.Unknown))
                {
                    using (ASGFileInfo theASG = new ASGFileInfo(theAttachment))
                    {
                        theActivity.GameType = theASG.GameType;
                    }
                }
            }
            else
            {
                using (ASGFileInfo theASG = new ASGFileInfo(theAttachment))
                {
                    theActivity = new Activity(
                        ActivityState.Sent,
                        theASG.GameType,
                        theASG.FileNameTrue,
                        theASG.MapTitle,
                        theASG.TurnNumber.ToString());
                    theActivity.AccountName = accountName;
                }

                _activityLog.Activities.Add(theActivity);
            }

            RaiseEvent(_activityLogRefresh, this, new EventArgs());

            return theActivity;
        }

        private void UpdateActivitySendError(MimePart theAttachment)
        {
            Activity theActivity = null;
            theActivity = _activityLog.GetLastActivityByFileName(theAttachment.FileName);

            if (theActivity != null)
            {
                theActivity.Status = ActivityState.Pending;
            }
            else
            {
                using (ASGFileInfo theASG = new ASGFileInfo(theAttachment))
                {
                    theActivity = new Activity(
                        ActivityState.Pending,
                        theASG.GameType,
                        theASG.FileNameTrue,
                        theASG.MapTitle,
                        theASG.TurnNumber.ToString());
                }

                _activityLog.Activities.Add(theActivity);
            }

            RaiseEvent(_activityLogRefresh, this, new EventArgs());
        }

        private void ActivityLogRefresh(object sender, EventArgs e)
        {
            activityListView.Refresh();
            DataManagerHelper.SaveActivityLog(_activityLog);
        }

        private void ActivityLogChanged(object sender, EventArgs e)
        {
            CheckNotifyIconState();
            DataManagerHelper.SaveActivityLog(_activityLog);
        }

        #endregion

        #region Context Menu

        private void CreateContextMenu()
        {
            EventHandler menuItemClickEvent = new EventHandler(menuItem_Click);
            if (_contextMenu != null)
            {
                //Settings were saved: rebuild the menu for the current copies of the games
                notifyIcon.ContextMenuStrip = null;
                _contextMenu.Dispose();
            }

            _contextMenu = new ContextMenuStrip();
            _contextMenu.Opening += new CancelEventHandler(ContextMenu_Popup);

            _menuShow = new ToolStripMenuItem(Translator.Translate(Menu_Show_Tag), null, menuItemClickEvent);
            _menuShow.Tag = Menu_Show_Tag;
            AddMenuItemToContextMenu(_menuShow);

            AddMenuItemToContextMenu(new ToolStripSeparator());

            _menuAccounts = new ToolStripMenuItem();            
            Image emailImage = imageListIcons.Images[IconState.EmailWaiting.ToString()];

            foreach (AowGame game in _gameManager.Games)
            {
                if (game.IsInstalled)
                {
                    string gameType = game.GameType.ToString();

                    IconMenuItem menuItem = new IconMenuItem(game.DisplayName, imageListIcons.Images[gameType], emailImage);
                    
                    menuItem.Name = game.Id;
                    menuItem.Tag = GameMenuTagPrefix + game.Id;
                    menuItem.Click += menuItemClickEvent;

                    AddMenuItemToContextMenu(menuItem);
                }
            }

            AddMenuItemToContextMenu(new ToolStripSeparator());

            _menuAccounts = new ToolStripMenuItem(Translator.Translate(Menu_Accounts_Tag));
            CreateAccountMenu(_menuAccounts, _wrapperConfig.AccountsList);
            AddMenuItemToContextMenu(_menuAccounts);

            _menuPoll = new ToolStripMenuItem(Translator.Translate(Menu_Poll_Tag), null, menuItemClickEvent);
            _menuPoll.Tag = Menu_Poll_Tag;
            _menuPoll.Enabled = false;
            AddMenuItemToContextMenu(_menuPoll);

            _menuExit = new ToolStripMenuItem(Translator.Translate(Menu_Exit_Tag), null, menuItemClickEvent);
            _menuExit.Tag = Menu_Exit_Tag;
            AddMenuItemToContextMenu(_menuExit);
          
            notifyIcon.ContextMenuStrip = _contextMenu;
        }

        private void AddMenuItemToContextMenu(ToolStripItem theItem)
        {
            _contextMenu.Items.Add(theItem);
        }

        private void menuItem_Click(object sender, EventArgs e)
        {
            string tag = ((ToolStripItem)sender).Tag.ToString();

            if (tag.StartsWith(GameMenuTagPrefix, StringComparison.Ordinal))
            {
                AowGame theGame = _gameManager.GetGameById(tag.Substring(GameMenuTagPrefix.Length));
                if (theGame != null)
                {
                    StartGame(theGame);
                }
                return;
            }

            switch (tag)
            {
                case Menu_Show_Tag:
                    Maximize();
                    break;
                case Menu_Poll_Tag:
                    PollAll();
                    break;
                case Menu_Exit_Tag:
                    RaiseEvent(_shutDownEvent, sender, new EventArgs());
                    break;
            }
        }

        private void CreateAccountMenu(ToolStripMenuItem parent, AccountConfigValuesList accountsList)
        {
            if (parent != null)
            {
                foreach (ToolStripItem sub in parent.DropDownItems.Cast<ToolStripItem>().ToList())
                {
                    sub.Dispose();
                }

                parent.DropDownItems.Clear();

                foreach (AccountConfigValues account in accountsList.Accounts)
                {
                    ToolStripMenuItem sub = new ToolStripMenuItem();
                    sub.Text = account.Name;
                    sub.Checked = account.IsActive;
                    sub.Click += new EventHandler(AccountMenu_Click);
                    parent.DropDownItems.Add(sub);
                }
            }
        }

        private void Rebuild_Account_Menu(object sender, EventArgs e)
        {
            CreateAccountMenu(_menuAccounts, _wrapperConfig.AccountsList);
        }

        private void AccountMenu_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem theMenu = (ToolStripMenuItem)sender;
            AccountConfigValues theAccount = _wrapperConfig.AccountsList.GetAccountByName(theMenu.Text);
            if (theAccount != null && theAccount.PollingConfig != null && !IsAnySending)
            {
                //The tray menu toggles whether an account is active (checks for email and replies through it)
                theAccount.PollingConfig.UsePolling = !theAccount.PollingConfig.UsePolling;
                accountsConfig.Config = _wrapperConfig.AccountsList;
                SaveConfig(true);
            }
        }

        private void ContextMenu_Popup(object sender, CancelEventArgs e)
        {
            _menuPoll.Enabled = _pollers.Count > 0;

            foreach (AowGame game in _gameManager.Games)
            {
                if (game.IsInstalled)
                {
                    if (_contextMenu.Items.ContainsKey(game.Id))
                    {
                        IconMenuItem menuItem = (IconMenuItem)_contextMenu.Items[game.Id];

                        int unknownGameTypeActivities = _activityLog.GetUnknownGameTypeActivitiesCount();
                        int unSentActivities = UnsentActivitiesFor(game);

                        if (unknownGameTypeActivities > 0 &&
                            (game.GameType.Equals(AowGameType.Aow2) || game.GameType.Equals(AowGameType.AowSm)))
                        {
                            menuItem.ShowEndImage = true;
                            menuItem.EndImage = imageListIcons.Images[IconState.CheckEmail.ToString()];
                        }
                        else if (unSentActivities > 0)
                        {
                            menuItem.ShowEndImage = true;
                            menuItem.EndImage = imageListIcons.Images[IconState.EmailWaiting.ToString()];
                        }
                        else
                        {
                            menuItem.ShowEndImage = false;
                        }
                    }
                }
            }

            foreach (ToolStripMenuItem sub in _menuAccounts.DropDownItems)
            {
                AccountConfigValues account = _wrapperConfig.AccountsList.GetAccountByName(sub.Text);
                sub.Checked = account != null && account.IsActive;
            }
        }

        #endregion
    }
}
