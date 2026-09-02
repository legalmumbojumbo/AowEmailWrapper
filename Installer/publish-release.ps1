# Publishes the freshly built installer as a GitHub pre-release for the current commit and prunes
# old build releases. Run by .github/workflows/build.yml on every push to master; it needs the
# GitHub CLI (gh) signed in, which the workflow provides through GH_TOKEN.
#
# Environment: COMMIT (full SHA, default: HEAD), RUN_NUMBER (default: commit count), KEEP_BUILDS (default 10).
#
# The Wrapper's update check reads the newest release, compares its target commit with the commit
# stamped into the running executable and downloads the attached *-setup.exe when it is newer.

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$commit = if ($env:COMMIT) { $env:COMMIT } else { (git -C $root rev-parse HEAD) }
$runNumber = if ($env:RUN_NUMBER) { $env:RUN_NUMBER } else { (git -C $root rev-list --count HEAD) }
$keep = if ($env:KEEP_BUILDS) { [int]$env:KEEP_BUILDS } else { 10 }

$installer = Get-ChildItem (Join-Path $root "publish\AowEmailWrapper-*-setup.exe") | Select-Object -First 1
if (-not $installer) { throw "No installer found in publish\. Run Installer\build-installer.ps1 first." }

$version = $installer.Name -replace '^AowEmailWrapper-(.+)-setup\.exe$', '$1'
$short = $commit.Substring(0, 7)
$tag = "v$version-build.$runNumber"
$title = "$version build $runNumber ($short)"
$subject = (git -C $root log -1 --format=%s $commit)
$notes = @"
Automatic build of commit $commit

    $subject

The Wrapper installs this itself through Check for updates on the Settings tab. To install by hand, download the setup below and run it.
"@

Write-Host "Creating release $tag for commit $commit"
gh release create $tag $installer.FullName --target $commit --prerelease --title $title --notes $notes
if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }

# Keep only the newest build releases; proper versioned releases (no "-build." in the tag) are never touched
$old = gh release list --limit 200 --json tagName,isPrerelease,createdAt `
    --jq "[.[] | select(.isPrerelease and (.tagName | contains(`"-build.`")))] | sort_by(.createdAt) | reverse | .[${keep}:] | .[].tagName"
if ($LASTEXITCODE -ne 0) { throw "gh release list failed" }

foreach ($oldTag in @($old | Where-Object { $_ })) {
    Write-Host "Deleting old build release $oldTag"
    gh release delete $oldTag --yes --cleanup-tag
    if ($LASTEXITCODE -ne 0) { Write-Warning "Could not delete $oldTag" }
}
