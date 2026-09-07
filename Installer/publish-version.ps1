# Publishes the freshly built installer as the versioned GitHub release for a v<version> tag, with the
# matching section of CHANGELOG.md as its notes. Run by .github/workflows/build.yml when a tag is
# pushed; it needs the GitHub CLI (gh) signed in, which the workflow provides through GH_TOKEN.
#
# Environment: TAG (default: the tag at HEAD), COMMIT (full SHA, default: HEAD).
#
# The tag must match the <Version> in the project, which is also the version in the installer's
# file name: tag v2.0.1 publishes AowEmailWrapper-2.0.1-setup.exe with the "## 2.0.1" notes.

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$commit = if ($env:COMMIT) { $env:COMMIT } else { (git -C $root rev-parse HEAD) }
$tag = if ($env:TAG) { $env:TAG } else { (git -C $root describe --tags --exact-match $commit) }
if ($tag -notmatch '^v(\d+\.\d+\.\d+)$') { throw "Tag '$tag' is not a version tag of the form v1.2.3" }
$version = $Matches[1]

$installer = Get-ChildItem (Join-Path $root "publish\AowEmailWrapper-*-setup.exe") | Select-Object -First 1
if (-not $installer) { throw "No installer found in publish\. Run Installer\build-installer.ps1 first." }
$built = $installer.Name -replace '^AowEmailWrapper-(.+)-setup\.exe$', '$1'
if ($built -ne $version) { throw "Tag $tag does not match the built version $built. Update <Version> in the project before tagging." }

# The notes are the changelog section for this version: from its "## <version>" heading to the next "## "
$changelog = Get-Content (Join-Path $root "CHANGELOG.md")
$start = [array]::IndexOf($changelog, ($changelog | Where-Object { $_ -match "^## $([regex]::Escape($version))\s*$" } | Select-Object -First 1))
if ($start -lt 0) { throw "CHANGELOG.md has no '## $version' section" }
$end = $start + 1
while ($end -lt $changelog.Count -and $changelog[$end] -notmatch '^## ') { $end++ }
$notes = ($changelog[($start + 1)..($end - 1)] -join "`n").Trim()
$notesFile = Join-Path $env:TEMP "release-notes-$version.md"
Set-Content $notesFile $notes -Encoding UTF8

$title = "Age of Wonders Email Wrapper $version"
Write-Host "Creating release $tag ($title) for commit $commit"
gh release create $tag $installer.FullName --target $commit --title $title --notes-file $notesFile --latest
if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
