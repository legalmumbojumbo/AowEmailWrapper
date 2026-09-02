using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using AowEmailWrapper.ConfigFramework;

namespace AowEmailWrapper.Games
{
    /// <summary>
    /// Finds every folder on the PC that holds an Age of Wonders executable. Nothing here depends on
    /// a single registry key: the Triumph Studios keys, every Steam library, GOG's registry, the
    /// Apps and Features uninstall entries and, in a deep scan, the fixed drives are all consulted,
    /// and nothing is written back. Copies found earlier are remembered in config, so a normal start
    /// only re-checks those plus the cheap sources; the deep scan runs on first use and on Rescan.
    /// </summary>
    public static class GameDetector
    {
        private const string TriumphRegPathTemplate = "Software\\Triumph Studios\\{0}\\General";
        private const string RootDirValueName = "Root Directory";
        private const string MostRecentlyUsedFileValueName = "Most Recently Used File";

        private const string SteamRegPath = "Software\\Valve\\Steam";
        private const string SteamPathValueName = "SteamPath";
        private const string SteamInstallPathValueName = "InstallPath";
        private const string SteamAppsFolder = "steamapps";
        private const string SteamCommonFolder = "common";
        private const string SteamLibraryFile = "libraryfolders.vdf";
        private const string SteamManifestTemplate = "appmanifest_{0}.acf";
        private static readonly Regex VdfPath = new Regex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex AcfInstallDir = new Regex("\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //Steam store ids of the three games; used to read the install folder from the app manifest
        private static readonly int[] SteamAppIds = { 61500, 61510, 61520 };

        private const string GogRegPath = "SOFTWARE\\GOG.com\\Games";
        private const string GogPathValueName = "path";

        private const string UninstallRegPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
        private const string DisplayNameValueName = "DisplayName";
        private const string InstallLocationValueName = "InstallLocation";
        private const string AgeOfWonders = "Age of Wonders";

        //Deep scan: how far down an ordinary folder tree to look, how much further inside anything that
        //looks like a game folder (mod copies are often nested several levels deep), and a ceiling on
        //the number of folders visited so an enormous drive cannot stall the Wrapper
        private const int NormalDepth = 3;
        private const int GameDepthBonus = 6;
        private const int MaxFoldersVisited = 60000;
        //Folder names worth following further down: the games themselves and the places games are kept
        private static readonly Regex GameLikeName = new Regex("age ?of ?wonders|aow|triumph|shadow ?magic|wonders|steamapps|^common$|games|gog|steam|mods?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly HashSet<string> SkippedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Windows", "$Recycle.Bin", "System Volume Information", "ProgramData", "Recovery", "Config.Msi",
            "WindowsApps", "PerfLogs", "AppData", "node_modules", ".git", "$WinREAgent", "MSOCache", "Intel", "AMD", "NVIDIA"
        };

        private sealed class Candidate
        {
            public string Folder;
            public InstallSource Source;
        }

        /// <summary>
        /// Every copy of every game that can be found, one entry per game type and folder, with the
        /// most trustworthy source first. Copies remembered in config are re-checked whether or not
        /// detection would find them again; folders the player added by hand keep their Manual source.
        /// With deepScan the fixed drives are walked as well.
        /// </summary>
        public static List<AowGame> Detect(IEnumerable<GameInstallConfigValues> known, bool deepScan)
        {
            List<AowGame> installs = new List<AowGame>();

            foreach (GameInstallConfigValues entry in (known ?? Enumerable.Empty<GameInstallConfigValues>()).Where(entry => entry.Manual))
            {
                AddFolder(installs, entry.Folder, InstallSource.Manual);
            }

            foreach (Candidate candidate in Candidates(deepScan))
            {
                AddFolder(installs, candidate.Folder, candidate.Source);
            }

            foreach (GameInstallConfigValues entry in (known ?? Enumerable.Empty<GameInstallConfigValues>()).Where(entry => !entry.Manual))
            {
                AddFolder(installs, entry.Folder, entry.Source);
            }

            return installs;
        }

        /// <summary>Installs found in one folder, for the Add folder button.</summary>
        public static List<AowGame> ScanFolder(string folder, InstallSource source)
        {
            List<AowGame> installs = new List<AowGame>();
            AddFolder(installs, folder, source);
            return installs;
        }

        /// <summary>Every game folder under a root, nested copies included, as the deep scan would see it.</summary>
        public static List<AowGame> ScanTree(string root)
        {
            List<AowGame> installs = new List<AowGame>();
            int budget = MaxFoldersVisited;
            foreach (Candidate candidate in Walk(root, 0, NormalDepth, ref budget))
            {
                AddFolder(installs, candidate.Folder, candidate.Source);
            }
            return installs;
        }

        private static void AddFolder(List<AowGame> installs, string folder, InstallSource source)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            string full;
            try
            {
                full = Path.GetFullPath(folder.Trim().Trim('"'));
            }
            catch (Exception)
            {
                return;
            }

            foreach (AowGameType type in AowGame.TypesInFolder(full))
            {
                if (!installs.Any(existing => existing.GameType == type && existing.IsFolder(full)))
                {
                    installs.Add(new AowGame(type, full, source));
                }
            }
        }

        #region Sources

        private static IEnumerable<Candidate> Candidates(bool deepScan)
        {
            List<Candidate> candidates = new List<Candidate>();

            Collect(candidates, RegistryCandidates, "Triumph Studios registry keys");
            Collect(candidates, SteamCandidates, "Steam");
            Collect(candidates, GogCandidates, "GOG");
            Collect(candidates, UninstallCandidates, "Apps and Features");
            if (deepScan)
            {
                Collect(candidates, DriveCandidates, "drive scan");
            }

            return candidates;
        }

        private static void Collect(List<Candidate> candidates, Func<IEnumerable<Candidate>> source, string name)
        {
            try
            {
                candidates.AddRange(source());
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Game detection through {0} failed: {1}", name, ex.Message);
            }
        }

        private static IEnumerable<Candidate> RegistryCandidates()
        {
            List<Candidate> found = new List<Candidate>();

            foreach (string gameName in new[] { AowGame.Aow1GameName, AowGame.Aow2GameName, AowGame.AowSmGameName })
            {
                string path = string.Format(TriumphRegPathTemplate, gameName);

                foreach (RegistryKey key in OpenAll(path))
                {
                    using (key)
                    {
                        string root = key.GetValue(RootDirValueName) as string;
                        if (!string.IsNullOrEmpty(root))
                        {
                            found.Add(new Candidate { Folder = root, Source = InstallSource.Registry });
                        }

                        //The game writes the last save it touched; its folder sits directly under the game folder
                        string recent = key.GetValue(MostRecentlyUsedFileValueName) as string;
                        if (!string.IsNullOrEmpty(recent))
                        {
                            string parent = SafeParent(SafeParent(recent));
                            if (!string.IsNullOrEmpty(parent))
                            {
                                found.Add(new Candidate { Folder = parent, Source = InstallSource.Registry });
                            }
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Every Steam library: the folders named in the app manifests, and then every folder under
        /// steamapps\common, because copies for mods are usually made right next to the Steam copy.
        /// </summary>
        private static IEnumerable<Candidate> SteamCandidates()
        {
            List<Candidate> found = new List<Candidate>();

            foreach (string library in SteamLibraries())
            {
                string steamApps = Path.Combine(library, SteamAppsFolder);
                string common = Path.Combine(steamApps, SteamCommonFolder);

                foreach (int appId in SteamAppIds)
                {
                    string manifest = Path.Combine(steamApps, string.Format(SteamManifestTemplate, appId));
                    if (File.Exists(manifest))
                    {
                        Match match = AcfInstallDir.Match(File.ReadAllText(manifest));
                        if (match.Success)
                        {
                            found.Add(new Candidate { Folder = Path.Combine(common, match.Groups[1].Value), Source = InstallSource.Steam });
                        }
                    }
                }

                int budget = MaxFoldersVisited;
                foreach (string gameFolder in SafeSubFolders(common))
                {
                    found.Add(new Candidate { Folder = gameFolder, Source = InstallSource.Steam });
                    //Nested copies inside a game folder, at any reasonable depth
                    if (GameLikeName.IsMatch(Path.GetFileName(gameFolder)))
                    {
                        foreach (Candidate nested in Walk(gameFolder, 1, NormalDepth + GameDepthBonus, ref budget))
                        {
                            nested.Source = InstallSource.Steam;
                            found.Add(nested);
                        }
                    }
                }
            }

            return found;
        }

        private static IEnumerable<string> SteamLibraries()
        {
            List<string> libraries = new List<string>();

            foreach (string steamPath in ReadValues(SteamRegPath, SteamPathValueName).Concat(ReadValues(SteamRegPath, SteamInstallPathValueName)))
            {
                string steam = steamPath.Replace('/', '\\');
                AddUnique(libraries, steam);

                string libraryFile = Path.Combine(steam, SteamAppsFolder, SteamLibraryFile);
                if (File.Exists(libraryFile))
                {
                    foreach (Match match in VdfPath.Matches(File.ReadAllText(libraryFile)))
                    {
                        AddUnique(libraries, match.Groups[1].Value.Replace("\\\\", "\\").Replace('/', '\\'));
                    }
                }
            }

            return libraries;
        }

        private static IEnumerable<Candidate> GogCandidates()
        {
            List<Candidate> found = new List<Candidate>();

            foreach (RegistryKey games in OpenAll(GogRegPath))
            {
                using (games)
                {
                    foreach (string name in games.GetSubKeyNames())
                    {
                        using (RegistryKey game = games.OpenSubKey(name))
                        {
                            string path = game != null ? game.GetValue(GogPathValueName) as string : null;
                            if (!string.IsNullOrEmpty(path))
                            {
                                found.Add(new Candidate { Folder = path, Source = InstallSource.Gog });
                            }
                        }
                    }
                }
            }

            return found;
        }

        private static IEnumerable<Candidate> UninstallCandidates()
        {
            List<Candidate> found = new List<Candidate>();

            foreach (RegistryKey uninstall in OpenAll(UninstallRegPath))
            {
                using (uninstall)
                {
                    foreach (string name in uninstall.GetSubKeyNames())
                    {
                        using (RegistryKey entry = uninstall.OpenSubKey(name))
                        {
                            if (entry == null)
                            {
                                continue;
                            }
                            string displayName = entry.GetValue(DisplayNameValueName) as string;
                            string location = entry.GetValue(InstallLocationValueName) as string;
                            if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(location) &&
                                displayName.IndexOf(AgeOfWonders, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                found.Add(new Candidate { Folder = location, Source = InstallSource.Uninstall });
                            }
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Walks every fixed drive a few levels deep, plus the Desktop and Documents, skipping
        /// Windows' own folders. Anything that looks like a game folder is followed much deeper so
        /// copies kept inside other copies are found too.
        /// </summary>
        private static IEnumerable<Candidate> DriveCandidates()
        {
            List<Candidate> found = new List<Candidate>();
            List<string> roots = new List<string>();

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                    {
                        AddUnique(roots, drive.RootDirectory.FullName);
                    }
                }
                catch (Exception)
                {
                    //A drive that cannot be queried is skipped
                }
            }

            AddUnique(roots, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            AddUnique(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

            Stopwatch timer = Stopwatch.StartNew();
            int budget = MaxFoldersVisited;
            foreach (string root in roots)
            {
                found.AddRange(Walk(root, 0, NormalDepth, ref budget));
            }
            Trace.TraceInformation("Drive scan visited {0} folders in {1} ms", MaxFoldersVisited - budget, timer.ElapsedMilliseconds);

            return found;
        }

        /// <summary>
        /// Depth limited walk. A folder whose name looks like a game, or that holds a game, extends
        /// the limit for everything below it. Reparse points (junctions, OneDrive placeholders) and
        /// Windows' own folders are not entered.
        /// </summary>
        private static List<Candidate> Walk(string folder, int depth, int maxDepth, ref int budget)
        {
            List<Candidate> found = new List<Candidate>();
            if (budget <= 0 || string.IsNullOrEmpty(folder))
            {
                return found;
            }

            budget--;
            found.Add(new Candidate { Folder = folder, Source = InstallSource.Folder });

            bool gameLike = AowGame.TypesInFolder(folder).Count > 0 || GameLikeName.IsMatch(Path.GetFileName(folder.TrimEnd('\\')) ?? string.Empty);
            if (gameLike)
            {
                maxDepth = Math.Max(maxDepth, depth + GameDepthBonus);
            }

            if (depth >= maxDepth)
            {
                return found;
            }

            foreach (DirectoryInfo sub in SafeSubFolderInfos(folder))
            {
                //Attributes come with the listing, so junctions and OneDrive placeholders cost nothing to skip
                if (SkippedFolders.Contains(sub.Name) || (sub.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }
                found.AddRange(Walk(sub.FullName, depth + 1, maxDepth, ref budget));
                if (budget <= 0)
                {
                    break;
                }
            }

            return found;
        }

        #endregion

        #region Helpers

        /// <summary>Opens a path under HKCU and under HKLM in both the 32 and 64 bit registry views.</summary>
        private static IEnumerable<RegistryKey> OpenAll(string path)
        {
            List<RegistryKey> keys = new List<RegistryKey>();

            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                        {
                            RegistryKey key = baseKey.OpenSubKey(path);
                            if (key != null)
                            {
                                keys.Add(key);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //A view that cannot be opened is skipped
                    }
                }
            }

            return keys;
        }

        private static IEnumerable<string> ReadValues(string path, string valueName)
        {
            List<string> values = new List<string>();
            foreach (RegistryKey key in OpenAll(path))
            {
                using (key)
                {
                    string value = key.GetValue(valueName) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        values.Add(value);
                    }
                }
            }
            return values;
        }

        private static IEnumerable<string> SafeSubFolders(string folder)
        {
            try
            {
                return !string.IsNullOrEmpty(folder) && Directory.Exists(folder) ? Directory.GetDirectories(folder) : new string[0];
            }
            catch (Exception)
            {
                return new string[0];
            }
        }

        private static IEnumerable<DirectoryInfo> SafeSubFolderInfos(string folder)
        {
            try
            {
                return !string.IsNullOrEmpty(folder) && Directory.Exists(folder) ? new DirectoryInfo(folder).EnumerateDirectories().ToList() : new List<DirectoryInfo>();
            }
            catch (Exception)
            {
                return new List<DirectoryInfo>();
            }
        }

        private static string SafeParent(string path)
        {
            try
            {
                return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AddUnique(List<string> list, string folder)
        {
            if (!string.IsNullOrWhiteSpace(folder) && !list.Any(existing => AowGame.SameFolder(existing, folder)))
            {
                list.Add(folder);
            }
        }

        #endregion
    }
}
