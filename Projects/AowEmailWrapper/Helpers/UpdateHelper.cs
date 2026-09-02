using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AowEmailWrapper.Helpers
{
    /// <summary>
    /// The git commit the running executable was built from. The project file stamps the commit
    /// into the assembly's informational version ("2.0.0+sha") and the commit date into an
    /// AssemblyMetadata attribute at build time.
    /// </summary>
    public class BuildInfo
    {
        public const string CommitDateMetadataKey = "CommitDate";

        public Version Version { get; set; }
        public string Commit { get; set; }
        public DateTimeOffset? CommitDate { get; set; }

        public string ShortCommit
        {
            get { return UpdateHelper.Shorten(Commit); }
        }

        /// <summary>"2.0.0": what players see on the About tab and in messages.</summary>
        public string DisplayVersion
        {
            get { return Version != null ? UpdateHelper.Normalize(Version).ToString() : "?"; }
        }

        /// <summary>"2.0.0 (abc1234)" for the log, or just "2.0.0" when the build was not made from a git checkout.</summary>
        public string Describe()
        {
            return string.IsNullOrEmpty(Commit) ? DisplayVersion : string.Format("{0} ({1})", DisplayVersion, ShortCommit);
        }
    }

    /// <summary>A GitHub release that carries a setup executable.</summary>
    public class UpdateInfo
    {
        public string Tag { get; set; }
        public string Name { get; set; }
        public string Commit { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
        public bool PreRelease { get; set; }
        public string ReleaseUrl { get; set; }
        public string Notes { get; set; }
        public string AssetName { get; set; }
        public string DownloadUrl { get; set; }
        public long Size { get; set; }
        public Version Version { get; set; }

        public string ShortCommit
        {
            get { return UpdateHelper.Shorten(Commit); }
        }

        /// <summary>"2.0.0 build 17 (abc1234)" style label for messages.</summary>
        public string Describe()
        {
            return !string.IsNullOrEmpty(Name) ? Name : Tag;
        }
    }

    /// <summary>
    /// Checks the GitHub releases of the repository named in the config file for a newer build
    /// than the running one, downloads its installer and hands it to the installer on exit.
    /// The CI workflow publishes a pre-release with the installer attached for every commit on
    /// master, so "newer" is decided by commit rather than by version number.
    /// </summary>
    public static class UpdateHelper
    {
        private const string RepositoryKey = "Update.Repository";
        private const string ReleasesApiTemplate = "https://api.github.com/repos/{0}/releases?per_page=10";
        private const string DownloadPrefixTemplate = "https://github.com/{0}/releases/download/";
        private const string InstallerSuffix = "-setup.exe";
        private const string UpdatesFolderName = "Updates";
        private const string NotifiedFileName = "notified.txt";
        // Inno Setup: no wizard pages, no reboot, and our own switch that makes it start the Wrapper again
        private const string InstallerArguments = "/SILENT /NORESTART /RELAUNCH=1";
        private const int CheckTimeoutSeconds = 30;
        private const int DownloadTimeoutMinutes = 15;
        private const int ShortCommitLength = 7;

        private static readonly Regex VersionPattern = new Regex(@"(\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.Compiled);
        private static readonly object _httpLock = new object();
        private static HttpClient _http;

        /// <summary>Installer to run once the main window has closed, set by the update flow.</summary>
        public static string PendingInstaller { get; set; }

        /// <summary>"owner/name" of the GitHub repository whose releases are checked, from the config file.</summary>
        public static string Repository
        {
            get
            {
                string value = ConfigHelper.GetProperty<string>(RepositoryKey, null);
                return !string.IsNullOrEmpty(value) ? value.Trim().Trim('/') : null;
            }
        }

        public static bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(Repository); }
        }

        public static BuildInfo CurrentBuild
        {
            get { return ReadBuildInfo(typeof(UpdateHelper).Assembly); }
        }

        public static string UpdatesFolder
        {
            get { return Path.Combine(AppDataHelper.Root.FullName, UpdatesFolderName); }
        }

        /// <summary>
        /// Tag of the last build the player was shown a balloon for, so a build they chose not to
        /// install is announced once rather than at every start.
        /// </summary>
        public static string LastNotifiedTag
        {
            get
            {
                try
                {
                    string file = Path.Combine(UpdatesFolder, NotifiedFileName);
                    return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("Could not read the last notified update: {0}", ex.Message);
                    return null;
                }
            }
            set
            {
                try
                {
                    Directory.CreateDirectory(UpdatesFolder);
                    File.WriteAllText(Path.Combine(UpdatesFolder, NotifiedFileName), value ?? string.Empty);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("Could not remember the notified update: {0}", ex.Message);
                }
            }
        }

        #region Build information

        public static BuildInfo ReadBuildInfo(Assembly assembly)
        {
            BuildInfo info = new BuildInfo();
            info.Version = assembly.GetName().Version;

            AssemblyInformationalVersionAttribute informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (informational != null && !string.IsNullOrEmpty(informational.InformationalVersion))
            {
                int plus = informational.InformationalVersion.IndexOf('+');
                if (plus >= 0 && plus < informational.InformationalVersion.Length - 1)
                {
                    info.Commit = informational.InformationalVersion.Substring(plus + 1).Trim();
                }
            }

            foreach (AssemblyMetadataAttribute metadata in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                DateTimeOffset date;
                if (BuildInfo.CommitDateMetadataKey.Equals(metadata.Key, StringComparison.Ordinal) &&
                    DateTimeOffset.TryParse(metadata.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    info.CommitDate = date;
                }
            }

            return info;
        }

        #endregion

        #region Checking

        /// <summary>
        /// Returns the newest release that is newer than the running build, or null when the
        /// running build is current. Throws on network or parsing errors.
        /// </summary>
        public static async Task<UpdateInfo> CheckAsync(CancellationToken cancellationToken)
        {
            string repository = Repository;
            if (string.IsNullOrEmpty(repository))
            {
                return null;
            }

            string url = string.Format(ReleasesApiTemplate, repository);
            Trace.TraceInformation("Checking for updates at {0}", url);

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(CheckTimeoutSeconds));

                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                    using (HttpResponseMessage response = await Client.SendAsync(request, timeout.Token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        string json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);

                        UpdateInfo latest = ParseLatestRelease(json);
                        BuildInfo build = CurrentBuild;

                        if (latest == null)
                        {
                            Trace.TraceInformation("No release with an installer found for {0}", repository);
                            return null;
                        }

                        if (!IsTrustedDownloadUrl(latest.DownloadUrl, repository))
                        {
                            throw new InvalidOperationException(string.Format("The installer link {0} does not point at the {1} releases.", latest.DownloadUrl, repository));
                        }

                        bool newer = IsNewer(latest, build);
                        Trace.TraceInformation("Latest release {0} (commit {1}, published {2}); running {3}; update {4}",
                            latest.Tag, latest.ShortCommit, latest.PublishedAt.ToString("u"), build.Describe(), newer ? "available" : "not needed");

                        return newer ? latest : null;
                    }
                }
            }
        }

        /// <summary>
        /// Picks the newest release in a GitHub "list releases" response that is not a draft and
        /// has a setup executable attached. The API returns releases newest first.
        /// </summary>
        public static UpdateInfo ParseLatestRelease(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                foreach (JsonElement release in document.RootElement.EnumerateArray())
                {
                    if (GetBool(release, "draft"))
                    {
                        continue;
                    }

                    JsonElement assets;
                    if (!release.TryGetProperty("assets", out assets) || assets.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement asset in assets.EnumerateArray())
                    {
                        string assetName = GetString(asset, "name");
                        string downloadUrl = GetString(asset, "browser_download_url");

                        if (string.IsNullOrEmpty(assetName) || string.IsNullOrEmpty(downloadUrl) ||
                            !assetName.EndsWith(InstallerSuffix, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        UpdateInfo info = new UpdateInfo();
                        info.Tag = GetString(release, "tag_name");
                        info.Name = GetString(release, "name");
                        info.Commit = GetString(release, "target_commitish");
                        info.PreRelease = GetBool(release, "prerelease");
                        info.ReleaseUrl = GetString(release, "html_url");
                        info.Notes = GetString(release, "body");
                        info.AssetName = assetName;
                        info.DownloadUrl = downloadUrl;
                        info.Size = GetLong(asset, "size");
                        info.Version = ParseVersion(assetName) ?? ParseVersion(info.Tag) ?? ParseVersion(info.Name);

                        DateTimeOffset published;
                        if (DateTimeOffset.TryParse(GetString(release, "published_at") ?? GetString(release, "created_at"),
                            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out published))
                        {
                            info.PublishedAt = published;
                        }

                        return info;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// A release is an update when it was built from a different commit and published after
        /// the running build's commit. Without commit information (a build made outside a git
        /// checkout) only a higher version number counts.
        /// </summary>
        public static bool IsNewer(UpdateInfo update, BuildInfo build)
        {
            if (update == null || build == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(build.Commit) && !string.IsNullOrEmpty(update.Commit))
            {
                if (SameCommit(update.Commit, build.Commit))
                {
                    return false;
                }

                if (build.CommitDate.HasValue && update.PublishedAt != default(DateTimeOffset))
                {
                    return update.PublishedAt > build.CommitDate.Value;
                }

                return true;
            }

            if (update.Version != null && build.Version != null)
            {
                return Normalize(update.Version) > Normalize(build.Version);
            }

            return false;
        }

        /// <summary>Only ever run an installer that GitHub serves from the configured repository's releases.</summary>
        public static bool IsTrustedDownloadUrl(string url, string repository)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(repository))
            {
                return false;
            }

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            string prefix = string.Format(DownloadPrefixTemplate, repository);
            return url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                   url.EndsWith(InstallerSuffix, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Downloading and installing

        /// <summary>
        /// Downloads the release's installer into the Wrapper's Updates folder and returns its path.
        /// Progress reports the number of bytes received so far.
        /// </summary>
        public static async Task<string> DownloadAsync(UpdateInfo update, IProgress<long> progress, CancellationToken cancellationToken)
        {
            if (update == null || string.IsNullOrEmpty(update.DownloadUrl))
            {
                throw new ArgumentException("No installer to download.");
            }

            if (!IsTrustedDownloadUrl(update.DownloadUrl, Repository))
            {
                throw new InvalidOperationException(string.Format("Refusing to download {0}: it is not a release of {1}.", update.DownloadUrl, Repository));
            }

            string folder = UpdatesFolder;
            Directory.CreateDirectory(folder);
            ClearFolder(folder, NotifiedFileName);

            string fileName = Path.GetFileName(update.AssetName);
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(InstallerSuffix, StringComparison.OrdinalIgnoreCase))
            {
                fileName = "AowEmailWrapper" + InstallerSuffix;
            }

            string target = Path.Combine(folder, fileName);
            string partial = target + ".partial";

            Trace.TraceInformation("Downloading {0} to {1}", update.DownloadUrl, target);

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(TimeSpan.FromMinutes(DownloadTimeoutMinutes));

                using (HttpResponseMessage response = await Client.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    long received = 0;
                    byte[] buffer = new byte[81920];

                    using (Stream source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false))
                    using (FileStream file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true))
                    {
                        int read;
                        while ((read = await source.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                        {
                            await file.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false);
                            received += read;
                            if (progress != null)
                            {
                                progress.Report(received);
                            }
                        }
                    }

                    if (update.Size > 0 && received != update.Size)
                    {
                        File.Delete(partial);
                        throw new IOException(string.Format("The download is incomplete: {0} of {1} bytes.", received, update.Size));
                    }
                }
            }

            File.Move(partial, target, true);
            return target;
        }

        /// <summary>
        /// Starts the installer saved by the update flow, if any. Called after the main window has
        /// closed so the installer never has to fight a running Wrapper.
        /// </summary>
        public static void RunPendingInstaller()
        {
            string installer = PendingInstaller;
            PendingInstaller = null;

            if (string.IsNullOrEmpty(installer) || !File.Exists(installer))
            {
                return;
            }

            try
            {
                Trace.TraceInformation("Starting installer {0} {1}", installer, InstallerArguments);
                Trace.Flush();
                Process.Start(new ProcessStartInfo(installer, InstallerArguments) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Trace.TraceError("Could not start the installer: {0}", ex);
            }
        }

        #endregion

        #region Helpers

        public static string Shorten(string commit)
        {
            if (string.IsNullOrEmpty(commit))
            {
                return commit;
            }
            return commit.Length > ShortCommitLength ? commit.Substring(0, ShortCommitLength) : commit;
        }

        /// <summary>Major.minor.build, so "2.0.0" from an asset name and "2.0.0.0" from the assembly compare equal.</summary>
        public static Version Normalize(Version version)
        {
            return new Version(Math.Max(version.Major, 0), Math.Max(version.Minor, 0), Math.Max(version.Build, 0));
        }

        public static Version ParseVersion(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            Match match = VersionPattern.Match(text);
            if (!match.Success)
            {
                return null;
            }

            int major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            int minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            int build = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
            return new Version(major, minor, build);
        }

        private static bool SameCommit(string a, string b)
        {
            //Either side may be abbreviated
            return a.StartsWith(b, StringComparison.OrdinalIgnoreCase) || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
        }

        private static HttpClient Client
        {
            get
            {
                lock (_httpLock)
                {
                    if (_http == null)
                    {
                        HttpClient http = new HttpClient();
                        http.Timeout = TimeSpan.FromMinutes(DownloadTimeoutMinutes);

                        //GitHub answers 403 to requests without a User-Agent. The product version must be a
                        //plain token, so the commit goes into the comment part along with the project link.
                        BuildInfo build = CurrentBuild;
                        string comment = string.IsNullOrEmpty(build.Commit)
                            ? string.Format("(+https://github.com/{0})", Repository ?? "davidhoness/AowEmailWrapper")
                            : string.Format("({0}; +https://github.com/{1})", build.ShortCommit, Repository ?? "davidhoness/AowEmailWrapper");
                        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AowEmailWrapper", build.DisplayVersion));
                        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(comment));

                        //Only keep the client once it is fully set up, so a failure here cannot leave a headerless one behind
                        _http = http;
                    }
                    return _http;
                }
            }
        }

        private static void ClearFolder(string folder, string keep)
        {
            foreach (string file in Directory.GetFiles(folder))
            {
                if (string.Equals(Path.GetFileName(file), keep, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("Could not delete old update file {0}: {1}", file, ex.Message);
                }
            }
        }

        private static string GetString(JsonElement element, string name)
        {
            JsonElement value;
            if (element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
            return null;
        }

        private static bool GetBool(JsonElement element, string name)
        {
            JsonElement value;
            return element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.True;
        }

        private static long GetLong(JsonElement element, string name)
        {
            JsonElement value;
            long result;
            if (element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out result))
            {
                return result;
            }
            return 0;
        }

        #endregion
    }
}
