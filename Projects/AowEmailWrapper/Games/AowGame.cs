using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Microsoft.Win32;
using AowEmailWrapper.ASG;
using AowEmailWrapper.Helpers;

namespace AowEmailWrapper.Games
{
    public enum AowGameType
    {
        [XmlEnum(Name = "Aow1")]
        Aow1 = 1,
        [XmlEnum(Name = "Aow2")]
        Aow2,
        [XmlEnum(Name = "AowSm")]
        AowSm,
        [XmlEnum(Name = "AowMpe")]
        AowMpe,
        [XmlEnum(Name = "Unknown")]
        Unknown
    }

    /// <summary>Where a game folder was found. Lower values are trusted more when picking a default.</summary>
    public enum InstallSource
    {
        [XmlEnum(Name = "Manual")]
        Manual = 0,
        [XmlEnum(Name = "Registry")]
        Registry,
        [XmlEnum(Name = "Steam")]
        Steam,
        [XmlEnum(Name = "Gog")]
        Gog,
        [XmlEnum(Name = "Uninstall")]
        Uninstall,
        [XmlEnum(Name = "Folder")]
        Folder
    }

    /// <summary>
    /// One installed copy of a game: a folder that holds the game's executable. A player may have
    /// several copies of the same game for different mods, each with its own label. The label is
    /// what travels in the mail header so the receiving Wrapper can put a turn in the right copy.
    /// </summary>
    public class AowGame
    {
        #region String Constants

        private const string AowRegPathTemplate = "Software\\Triumph Studios\\{0}";
        public const string Aow1GameName = "Age of Wonders";
        public const string Aow2GameName = "Age of Wonders II";
        public const string AowSmGameName = "Age of Wonders Shadow Magic";
        public const string AowMpeGameName = "AoW - MP Evolution";

        private const string EmailPath = "Email";
        private const string AttachmentDirKeyName = "Attachment Directory";
        private const string LocalEmailKeyName = "Local Email address";
        private const string SMTPServerKeyName = "SMTP Server";

        public const string Aow1ExeName = "AoW.exe";
        public const string Aow2ExeName = "AoW2.exe";
        public const string AowSmExeName = "AoWSM.exe";
        public const string AowMpeExeName = "AoW - MP Evolution.exe";

        private const string DummyTestFileTemplate = "{0}.asg";
        private const string FileSearchTemplate = "*{0}*.asg";

        private const string EmailInFolder = "EmailIn";
        private const string EmailOutFolder = "EmailOut";
        private const string SaveFolder = "Save";

        public static readonly AowGameType[] AllTypes = { AowGameType.Aow1, AowGameType.Aow2, AowGameType.AowSm, AowGameType.AowMpe };

        private static readonly Regex LabelNoise = new Regex(@"[^\p{L}\p{Nd}]+", RegexOptions.Compiled);

        #endregion

        #region Private Members

        private readonly AowGameType _gameType;
        private readonly InstallSource _source;
        private readonly bool _isInstalled;
        private readonly DirectoryInfo _root;
        private readonly DirectoryInfo _emailIn;
        private readonly DirectoryInfo _emailOut;
        private readonly DirectoryInfo _save;
        private readonly string _exeFile;
        private readonly string _gameName;
        private bool _writeAccess;
        private bool _writeAccessChecked;
        private string _label = string.Empty;

        #endregion

        #region Public Properties

        public AowGameType GameType
        {
            get { return _gameType; }
        }

        /// <summary>True when the game's executable exists in the folder.</summary>
        public bool IsInstalled
        {
            get { return _isInstalled; }
        }

        public InstallSource Source
        {
            get { return _source; }
        }

        public bool IsManual
        {
            get { return _source == InstallSource.Manual; }
        }

        /// <summary>Short mod name chosen by the player, for example "Ziggurat". Empty for an unlabelled copy.</summary>
        public string Label
        {
            get { return _label; }
            set { _label = (value ?? string.Empty).Trim(); }
        }

        /// <summary>The copy that receives turns carrying no (or an unknown) label and that the games are pointed at.</summary>
        public bool IsDefault { get; set; }

        public DirectoryInfo Root
        {
            get { return _root; }
        }

        public string Folder
        {
            get { return _root.FullName; }
        }

        public DirectoryInfo EmailIn
        {
            get { return _emailIn; }
        }

        public DirectoryInfo EmailOut
        {
            get { return _emailOut; }
        }

        public DirectoryInfo Save
        {
            get { return _save; }
        }

        public string ExeFile
        {
            get { return _exeFile; }
        }

        public string ExePath
        {
            get { return Path.Combine(_root.FullName, _exeFile); }
        }

        /// <summary>The registry name the game uses; MP Evolution shares Shadow Magic's.</summary>
        public string GameName
        {
            get { return _gameName; }
        }

        /// <summary>"Age of Wonders Shadow Magic (Ziggurat)" for the tray menu and lists.</summary>
        public string DisplayName
        {
            get
            {
                string name = DisplayNameFor(_gameType);
                return string.IsNullOrEmpty(_label) ? name : string.Format("{0} ({1})", name, _label);
            }
        }

        /// <summary>Stable identity for menus and lists: game type plus folder.</summary>
        public string Id
        {
            get { return string.Concat(_gameType, "|", NormalizeFolder(_root.FullName)); }
        }

        public bool WriteAccess
        {
            get
            {
                if (!_writeAccessChecked)
                {
                    _writeAccessChecked = true;
                    _writeAccess = _isInstalled && CheckWriteAccess();
                }
                return _writeAccess;
            }
        }

        #endregion

        /// <summary>Forgets the cached write check, for after permissions were changed.</summary>
        public void ResetWriteAccess()
        {
            _writeAccessChecked = false;
        }

        #region Constructors

        public AowGame(AowGameType theGameType, string folder, InstallSource source)
        {
            _gameType = theGameType;
            _source = source;
            _exeFile = ExeFor(theGameType);
            _gameName = GameNameFor(theGameType);

            _root = new DirectoryInfo(folder);
            _isInstalled = _root.Exists && File.Exists(Path.Combine(_root.FullName, _exeFile));

            string emailInFolder = Path.Combine(_root.FullName, EmailInFolder);
            string emailOutFolder = Path.Combine(_root.FullName, EmailOutFolder);
            string saveFolder = Path.Combine(_root.FullName, SaveFolder);

            _emailIn = Directory.Exists(emailInFolder) ? new DirectoryInfo(emailInFolder) : _root;
            _emailOut = Directory.Exists(emailOutFolder) ? new DirectoryInfo(emailOutFolder) : _root;
            _save = Directory.Exists(saveFolder) ? new DirectoryInfo(saveFolder) : _emailIn;
        }

        #endregion

        #region Static helpers

        public static string ExeFor(AowGameType type)
        {
            switch (type)
            {
                case AowGameType.Aow1: return Aow1ExeName;
                case AowGameType.Aow2: return Aow2ExeName;
                case AowGameType.AowSm: return AowSmExeName;
                case AowGameType.AowMpe: return AowMpeExeName;
                default: return null;
            }
        }

        public static string GameNameFor(AowGameType type)
        {
            switch (type)
            {
                case AowGameType.Aow1: return Aow1GameName;
                case AowGameType.Aow2: return Aow2GameName;
                case AowGameType.AowSm:
                case AowGameType.AowMpe:
                    //MP Evolution is a patch of Shadow Magic with its own exe; it reads Shadow Magic's registry settings
                    return AowSmGameName;
                default: return null;
            }
        }

        public static string DisplayNameFor(AowGameType type)
        {
            return type == AowGameType.AowMpe ? AowMpeGameName : GameNameFor(type);
        }

        /// <summary>The game types whose executable is present in the folder.</summary>
        public static List<AowGameType> TypesInFolder(string folder)
        {
            List<AowGameType> types = new List<AowGameType>();
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                foreach (AowGameType type in AllTypes)
                {
                    if (File.Exists(Path.Combine(folder, ExeFor(type))))
                    {
                        types.Add(type);
                    }
                }
            }
            return types;
        }

        /// <summary>"Zig Mod" and "zigmod" are the same label: players only have to agree on the spelling.</summary>
        public static string NormalizeLabel(string label)
        {
            return string.IsNullOrEmpty(label) ? string.Empty : LabelNoise.Replace(label, string.Empty).ToLowerInvariant();
        }

        public static bool SameLabel(string a, string b)
        {
            return NormalizeLabel(a) == NormalizeLabel(b);
        }

        public static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return string.Empty;
            }
            try
            {
                return Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
            }
            catch (Exception)
            {
                return folder.Trim().ToLowerInvariant();
            }
        }

        public static bool SameFolder(string a, string b)
        {
            return NormalizeFolder(a) == NormalizeFolder(b);
        }

        #endregion

        #region Public Methods

        public bool IsFolder(string folder)
        {
            return SameFolder(_root.FullName, folder);
        }

        /// <summary>Writes the game's email settings so this game hands its turns to the Wrapper.</summary>
        public void SetEmailConfig(string attachmentDir, string localEmailAddress, string smtpServer)
        {
            if (!_isInstalled)
            {
                return;
            }

            RegistryKey rootRegKey = RegistryHelper.GetDeepestKey(Registry.CurrentUser, string.Format(AowRegPathTemplate, _gameName), true);
            if (rootRegKey == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(attachmentDir))
            {
                RegistryHelper.SetValue(rootRegKey, EmailPath, AttachmentDirKeyName, attachmentDir);
            }
            if (!string.IsNullOrEmpty(localEmailAddress))
            {
                RegistryHelper.SetValue(rootRegKey, EmailPath, LocalEmailKeyName, localEmailAddress);
            }
            if (!string.IsNullOrEmpty(smtpServer))
            {
                RegistryHelper.SetValue(rootRegKey, EmailPath, SMTPServerKeyName, smtpServer);
            }
        }

        /// <summary>The folders a turn may sit in, without duplicates when the game has no sub folders.</summary>
        public IEnumerable<DirectoryInfo> TurnFolders
        {
            get
            {
                List<DirectoryInfo> folders = new List<DirectoryInfo>();
                foreach (DirectoryInfo folder in new[] { _emailIn, _emailOut, _save })
                {
                    if (!folders.Any(existing => SameFolder(existing.FullName, folder.FullName)))
                    {
                        folders.Add(folder);
                    }
                }
                return folders;
            }
        }

        public static string SearchPattern(string fileName)
        {
            return string.Format(FileSearchTemplate, ASGFileInfo.SafeSearchFileName(fileName));
        }

        /// <summary>True when an earlier turn of the game is already in this copy's folders.</summary>
        public bool HoldsGameFile(string fileName)
        {
            if (!_isInstalled || string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            string pattern = SearchPattern(fileName);
            foreach (DirectoryInfo folder in TurnFolders)
            {
                try
                {
                    if (folder.Exists && folder.GetFiles(pattern).Length > 0)
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    //An unreadable folder simply does not count
                }
            }
            return false;
        }

        public override string ToString()
        {
            return string.Format("{0} [{1}] {2}", DisplayName, _source, _root.FullName);
        }

        #endregion

        #region Private Methods

        private bool CheckWriteAccess()
        {
            bool writeAccess = WritePermission(_emailIn) && WritePermission(_save);

            if (!writeAccess && Environment.OSVersion.Version.Major >= 6) //Vista or later
            {
                //Windows User Account Control (UAC) is probably on
                //See: C:\Users\*UserName*\AppData\Local\VirtualStore\Program Files (x86)\Age of Wonders\EmailIn
                FileVirtualizationHelper.Enable();
                writeAccess = WritePermission(_emailIn) && WritePermission(_save);
            }

            return writeAccess;
        }

        private bool WritePermission(DirectoryInfo folder)
        {
            try
            {
                string testFile = Path.Combine(folder.FullName, string.Format(DummyTestFileTemplate, Guid.NewGuid().ToString()));
                File.WriteAllBytes(testFile, new byte[] { 1, 2, 3 });
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
