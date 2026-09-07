using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AowEmailWrapper.Games
{
    /// <summary>A mod found in a game folder, with the file that gave it away.</summary>
    public class ModInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Evidence { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Version) ? Name : string.Format("{0} {1}", Name, Version);
        }
    }

    /// <summary>
    /// Recognises the Age of Wonders 1 mods the community plays from what they leave in the game
    /// folder. Each rule was checked against the mods' own installers and against real copies:
    ///  - Ziggurat replaces the game's text tables, and its Dict\ResStr.txt version line reads
    ///    "Version: Ziggurat %s" in every release seen (stock reads "Version: Evolved %s"); releases
    ///    from 2026 also ship a README.txt whose first line names the mod and a "Release" date.
    ///  - AoWx patches the executable, replacing the Triumph copyright string with
    ///    "Age of Wonders X mod by IniochReborn" and moving saves into an X subfolder; its installer
    ///    (Inno Setup) registers "Age of Wonders X" in Apps and Features with the version.
    ///  - Dark Lord ships readme_DarkLordMod.txt.
    /// </summary>
    public static class ModDetector
    {
        public const string Ziggurat = "Ziggurat";
        public const string AowX = "AoWx";
        public const string DarkLord = "Dark Lord";

        /// <summary>AoWx keeps campaign and save files under this subfolder of the game folder.</summary>
        public const string AowXSubfolder = "X";

        private const string DictFolder = "Dict";
        private const string ResourceStringsFile = "ResStr.txt";
        private const string ZigguratVersionMarker = "Version: Ziggurat";
        private const string ZigguratReadme = "README.txt";
        private const string AowXDisplayName = "Age of Wonders X";
        private const string DarkLordReadme = "readme_DarkLordMod.txt";

        private static readonly byte[] AowXExeMarker = Encoding.ASCII.GetBytes("Age of Wonders X mod");
        private static readonly string[] ExeNames = { "AoWx.exe", "AoW.exe", "AoWCompat.exe" };
        private static readonly Regex ReleaseDate = new Regex(@"Release\s+(\d{4}-\d{2}-\d{2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<ModInfo> Detect(string folder)
        {
            List<ModInfo> mods = new List<ModInfo>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return mods;
            }

            try
            {
                ModInfo ziggurat = DetectZiggurat(folder);
                if (ziggurat != null)
                {
                    mods.Add(ziggurat);
                }

                ModInfo aowx = DetectAowX(folder);
                if (aowx != null)
                {
                    mods.Add(aowx);
                }

                if (File.Exists(Path.Combine(folder, DarkLordReadme)))
                {
                    mods.Add(new ModInfo { Name = DarkLord, Evidence = DarkLordReadme });
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Mod detection in {0} failed: {1}", folder, ex.Message);
            }

            return mods;
        }

        private static ModInfo DetectZiggurat(string folder)
        {
            string resources = Path.Combine(folder, DictFolder, ResourceStringsFile);
            if (!File.Exists(resources) || !ContainsText(resources, ZigguratVersionMarker))
            {
                return null;
            }

            ModInfo mod = new ModInfo { Name = Ziggurat, Evidence = Path.Combine(DictFolder, ResourceStringsFile) + " says " + ZigguratVersionMarker };

            string readme = Path.Combine(folder, ZigguratReadme);
            if (File.Exists(readme))
            {
                string release = ZigguratRelease(ReadText(readme));
                if (release != null)
                {
                    mod.Version = release;
                    mod.Evidence += "; " + ZigguratReadme + " release " + release;
                }
            }

            return mod;
        }

        /// <summary>The release date from a Ziggurat README, or null when the text is not one.</summary>
        public static string ZigguratRelease(string readmeText)
        {
            if (string.IsNullOrEmpty(readmeText))
            {
                return null;
            }

            string firstLine = readmeText.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0) ?? string.Empty;
            if (firstLine.IndexOf(Ziggurat, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return null;
            }

            Match match = ReleaseDate.Match(readmeText);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static ModInfo DetectAowX(string folder)
        {
            foreach (string exeName in ExeNames)
            {
                string exe = Path.Combine(folder, exeName);
                if (File.Exists(exe) && ContainsBytes(exe, AowXExeMarker))
                {
                    return new ModInfo
                    {
                        Name = AowX,
                        Version = GameDetector.InstalledVersion(folder, AowXDisplayName),
                        Evidence = exeName + " is branded \"" + Encoding.ASCII.GetString(AowXExeMarker) + "\""
                    };
                }
            }
            return null;
        }

        #region File helpers

        private static string ReadText(string path)
        {
            //The game's text files are Windows-1252, and only ASCII markers are looked for
            return File.ReadAllText(path, Encoding.Latin1);
        }

        private static bool ContainsText(string path, string marker)
        {
            return ReadText(path).IndexOf(marker, StringComparison.Ordinal) >= 0;
        }

        public static bool ContainsBytes(string path, byte[] marker)
        {
            byte[] data = File.ReadAllBytes(path);
            return data.AsSpan().IndexOf(marker) >= 0;
        }

        #endregion
    }
}
