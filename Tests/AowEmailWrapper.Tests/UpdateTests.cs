using System;
using System.Text.RegularExpressions;
using AowEmailWrapper.Helpers;
using Xunit;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// The update check against GitHub releases: parsing the API response, deciding whether a
    /// release is newer than the running build, and the build stamp in the assembly.
    /// </summary>
    public class UpdateTests
    {
        private const string Repository = "legalmumbojumbo/AowEmailWrapper";

        private const string ReleasesJson = @"[
          {
            ""tag_name"": ""v2.1.0-build.30"", ""name"": ""2.1.0 build 30 (draft)"", ""draft"": true, ""prerelease"": true,
            ""target_commitish"": ""ffffffffffffffffffffffffffffffffffffffff"", ""published_at"": null,
            ""html_url"": ""https://github.com/legalmumbojumbo/AowEmailWrapper/releases/tag/v2.1.0-build.30"",
            ""assets"": [ { ""name"": ""AowEmailWrapper-2.1.0-setup.exe"", ""size"": 1, ""browser_download_url"": ""https://github.com/legalmumbojumbo/AowEmailWrapper/releases/download/v2.1.0-build.30/AowEmailWrapper-2.1.0-setup.exe"" } ]
          },
          {
            ""tag_name"": ""notes-only"", ""name"": ""No installer attached"", ""draft"": false, ""prerelease"": false,
            ""target_commitish"": ""eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"", ""published_at"": ""2026-09-02T12:00:00Z"",
            ""html_url"": ""https://github.com/legalmumbojumbo/AowEmailWrapper/releases/tag/notes-only"",
            ""assets"": [ { ""name"": ""results.zip"", ""size"": 5, ""browser_download_url"": ""https://github.com/legalmumbojumbo/AowEmailWrapper/releases/download/notes-only/results.zip"" } ]
          },
          {
            ""tag_name"": ""v2.0.0-build.29"", ""name"": ""2.0.0 build 29 (abc1234)"", ""draft"": false, ""prerelease"": true,
            ""target_commitish"": ""abc1234abc1234abc1234abc1234abc1234abc12"", ""published_at"": ""2026-09-01T10:15:00Z"",
            ""html_url"": ""https://github.com/legalmumbojumbo/AowEmailWrapper/releases/tag/v2.0.0-build.29"",
            ""body"": ""Automatic build of commit abc1234"",
            ""assets"": [
              { ""name"": ""results.trx"", ""size"": 7, ""browser_download_url"": ""https://github.com/legalmumbojumbo/AowEmailWrapper/releases/download/v2.0.0-build.29/results.trx"" },
              { ""name"": ""AowEmailWrapper-2.0.0-setup.exe"", ""size"": 5242880, ""browser_download_url"": ""https://github.com/legalmumbojumbo/AowEmailWrapper/releases/download/v2.0.0-build.29/AowEmailWrapper-2.0.0-setup.exe"" }
            ]
          },
          {
            ""tag_name"": ""v2.0.0-build.28"", ""name"": ""2.0.0 build 28"", ""draft"": false, ""prerelease"": true,
            ""target_commitish"": ""0000000000000000000000000000000000000000"", ""published_at"": ""2026-08-30T10:15:00Z"",
            ""assets"": [ { ""name"": ""AowEmailWrapper-2.0.0-setup.exe"", ""size"": 1, ""browser_download_url"": ""https://github.com/legalmumbojumbo/AowEmailWrapper/releases/download/v2.0.0-build.28/AowEmailWrapper-2.0.0-setup.exe"" } ]
          }
        ]";

        [Fact]
        public void ParseLatestRelease_SkipsDraftsAndReleasesWithoutInstaller()
        {
            UpdateInfo latest = UpdateHelper.ParseLatestRelease(ReleasesJson);

            Assert.NotNull(latest);
            Assert.Equal("v2.0.0-build.29", latest.Tag);
            Assert.Equal("2.0.0 build 29 (abc1234)", latest.Name);
            Assert.Equal("abc1234abc1234abc1234abc1234abc1234abc12", latest.Commit);
            Assert.Equal("abc1234", latest.ShortCommit);
            Assert.True(latest.PreRelease);
            Assert.Equal(new DateTimeOffset(2026, 9, 1, 10, 15, 0, TimeSpan.Zero), latest.PublishedAt);
            Assert.Equal("AowEmailWrapper-2.0.0-setup.exe", latest.AssetName);
            Assert.Equal(5242880, latest.Size);
            Assert.Equal(new Version(2, 0, 0), latest.Version);
            Assert.EndsWith("/v2.0.0-build.29/AowEmailWrapper-2.0.0-setup.exe", latest.DownloadUrl);
            Assert.Equal("Automatic build of commit abc1234", latest.Notes);
        }

        [Theory]
        [InlineData("")]
        [InlineData("[]")]
        [InlineData("{\"message\":\"Not Found\"}")]
        public void ParseLatestRelease_ReturnsNullWhenThereIsNothingUsable(string json)
        {
            Assert.Null(UpdateHelper.ParseLatestRelease(json));
        }

        [Fact]
        public void IsNewer_SameCommitIsNotAnUpdate()
        {
            UpdateInfo release = Release("abc1234abc1234abc1234abc1234abc1234abc12", "2026-09-01T10:15:00Z");
            BuildInfo build = Build("abc1234abc1234abc1234abc1234abc1234abc12", "2026-09-01T10:00:00Z");

            Assert.False(UpdateHelper.IsNewer(release, build));

            //Either side may be abbreviated
            build.Commit = "abc1234";
            Assert.False(UpdateHelper.IsNewer(release, build));
        }

        [Fact]
        public void IsNewer_LaterCommitIsAnUpdate()
        {
            UpdateInfo release = Release("1111111111111111111111111111111111111111", "2026-09-02T09:00:00Z");
            BuildInfo build = Build("abc1234abc1234abc1234abc1234abc1234abc12", "2026-09-01T10:00:00Z");

            Assert.True(UpdateHelper.IsNewer(release, build));
        }

        [Fact]
        public void IsNewer_LocalBuildAheadOfLatestReleaseIsNotOfferedTheOlderRelease()
        {
            UpdateInfo release = Release("abc1234abc1234abc1234abc1234abc1234abc12", "2026-09-01T10:15:00Z");
            BuildInfo build = Build("1111111111111111111111111111111111111111", "2026-09-02T09:00:00Z");

            Assert.False(UpdateHelper.IsNewer(release, build));
        }

        [Fact]
        public void IsNewer_DifferentCommitWithoutDatesCountsAsUpdate()
        {
            UpdateInfo release = Release("1111111111111111111111111111111111111111", null);
            BuildInfo build = Build("abc1234abc1234abc1234abc1234abc1234abc12", null);

            Assert.True(UpdateHelper.IsNewer(release, build));
        }

        [Fact]
        public void IsNewer_WithoutCommitInformationOnlyAHigherVersionCounts()
        {
            BuildInfo build = new BuildInfo { Version = new Version(2, 0, 0, 0) };

            UpdateInfo same = Release("1111111111111111111111111111111111111111", "2026-09-02T09:00:00Z");
            same.Version = new Version(2, 0, 0);
            Assert.False(UpdateHelper.IsNewer(same, build));

            UpdateInfo higher = Release("1111111111111111111111111111111111111111", "2026-09-02T09:00:00Z");
            higher.Version = new Version(2, 0, 1);
            Assert.True(UpdateHelper.IsNewer(higher, build));

            UpdateInfo lower = Release("1111111111111111111111111111111111111111", "2026-09-02T09:00:00Z");
            lower.Version = new Version(1, 9, 0);
            Assert.False(UpdateHelper.IsNewer(lower, build));

            Assert.False(UpdateHelper.IsNewer(null, build));
            Assert.False(UpdateHelper.IsNewer(higher, null));
        }

        [Theory]
        [InlineData("https://github.com/legalmumbojumbo/AowEmailWrapper/releases/download/v2.0.0-build.29/AowEmailWrapper-2.0.0-setup.exe", true)]
        [InlineData("http://github.com/legalmumbojumbo/AowEmailWrapper/releases/download/v2.0.0-build.29/AowEmailWrapper-2.0.0-setup.exe", false)]
        [InlineData("https://github.com/someone-else/AowEmailWrapper/releases/download/v2.0.0-build.29/AowEmailWrapper-2.0.0-setup.exe", false)]
        [InlineData("https://github.com/legalmumbojumbo/AowEmailWrapper/releases/download/v2.0.0-build.29/AowEmailWrapper.zip", false)]
        [InlineData("https://evil.example/AowEmailWrapper-2.0.0-setup.exe", false)]
        [InlineData("", false)]
        public void IsTrustedDownloadUrl_OnlyAcceptsInstallersFromTheConfiguredRepository(string url, bool expected)
        {
            Assert.Equal(expected, UpdateHelper.IsTrustedDownloadUrl(url, Repository));
        }

        [Theory]
        [InlineData("AowEmailWrapper-2.0.0-setup.exe", "2.0.0")]
        [InlineData("v2.1.3-build.17", "2.1.3")]
        [InlineData("2.5 build 4", "2.5.0")]
        [InlineData("no version here", null)]
        public void ParseVersion_FindsTheFirstDottedNumber(string text, string expected)
        {
            Version parsed = UpdateHelper.ParseVersion(text);
            Assert.Equal(expected == null ? null : new Version(expected), parsed);
        }

        [Fact]
        public void CurrentBuild_CarriesTheAssemblyVersionAndAFullCommitWhenBuiltFromGit()
        {
            BuildInfo build = UpdateHelper.CurrentBuild;

            Assert.NotNull(build.Version);
            Assert.Equal(new Version(2, 0, 0), UpdateHelper.Normalize(build.Version));

            if (!string.IsNullOrEmpty(build.Commit))
            {
                Assert.Matches(new Regex("^[0-9a-f]{40}$"), build.Commit);
                Assert.True(build.CommitDate.HasValue, "a build with a commit should also carry the commit date");
                Assert.Equal(build.Describe(), string.Format("2.0.0 ({0})", build.Commit.Substring(0, 7)));
            }
        }

        private static UpdateInfo Release(string commit, string publishedAt)
        {
            UpdateInfo info = new UpdateInfo();
            info.Tag = "v2.0.0-build.1";
            info.Commit = commit;
            info.Version = new Version(2, 0, 0);
            if (publishedAt != null)
            {
                info.PublishedAt = DateTimeOffset.Parse(publishedAt, System.Globalization.CultureInfo.InvariantCulture);
            }
            return info;
        }

        private static BuildInfo Build(string commit, string commitDate)
        {
            BuildInfo info = new BuildInfo();
            info.Version = new Version(2, 0, 0, 0);
            info.Commit = commit;
            if (commitDate != null)
            {
                info.CommitDate = DateTimeOffset.Parse(commitDate, System.Globalization.CultureInfo.InvariantCulture);
            }
            return info;
        }
    }
}
