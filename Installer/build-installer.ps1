# Publishes the wrapper (framework-dependent, 64-bit) and builds the Inno Setup installer.
# Requires the .NET 8 SDK and Inno Setup 6 (https://jrsoftware.org/isinfo.php).
# Output: publish\AowEmailWrapper-<version>-setup.exe

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "Projects\AowEmailWrapper\AowEmailWrapper.csproj"
$publishDir = Join-Path $root "publish\win-x64"

# Version comes from the project file so the installer and the About page agree
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> found in $project" }

Write-Host "Publishing version $version to $publishDir"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained false -o $publishDir -nologo -v:q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it from https://jrsoftware.org/isinfo.php (or: winget install JRSoftware.InnoSetup)" }

Write-Host "Building installer with $iscc"
& $iscc "/DMyAppVersion=$version" (Join-Path $PSScriptRoot "AowEmailWrapper.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

Get-ChildItem (Join-Path $root "publish\AowEmailWrapper-$version-setup.exe") | ForEach-Object {
    Write-Host ("Installer: {0} ({1:N1} MB)" -f $_.FullName, ($_.Length / 1MB))
}
