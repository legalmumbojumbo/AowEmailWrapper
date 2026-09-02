Age of Wonders Email Wrapper
=================

Play-by-email helper for Age of Wonders 1, Age of Wonders 2, Shadow Magic and MP Evolution.
The wrapper runs a tiny SMTP listener on 127.0.0.1 that the games talk to, relays each turn
through your real mail account, and polls that account (IMAP or POP3) for incoming turns.

Originally written by David Honess (migrated from a personal SVN repo); version 2.0 by Eugene Wolff. Feel free to fork.

[binaries of the original 1.x release on Aow Heaven](http://aow.heavengames.com/downloads/showfile.php?fileid=1122)

Version 2.0
-----------

Version 2.0 is a port of the 2013 code base to modern .NET so that it works with current mail
providers:

- Targets .NET 8 (Windows Forms) instead of .NET Framework 3.5, which gives TLS 1.2 / 1.3.
- Mail transport is now [MailKit / MimeKit](https://github.com/jstedfast/MailKit) (MIT licensed);
  the commercial Mail.dll and its license file are gone.
- The Visual Studio installer project and the uninstall custom action were dropped.

- The account wizard warns when an address belongs to Gmail, Yahoo/AOL or Outlook.com and links to
  the page where an app password is created. Providers that only allow OAuth sign-in are flagged.
- The Incoming and Outgoing tabs have a Test connection button that signs in with the settings shown
  and reports the exact server response when it fails.
- IMAP accounts use IMAP IDLE: the server announces new mail immediately instead of the wrapper
  polling every ten minutes, and only messages that actually carry a save game are downloaded.
- Errors are logged to `%APPDATA%\AowEmailWrapper\Logs\wrapper.log` (*Open log folder* on the Settings tab).
- *Report a bug* on the Settings tab emails a description and the log to the maintainer through the
  player's own account (address in `App.config`, key `BugReport.Email`).
- Passwords in config.xml are now encrypted with Windows DPAPI (tied to your Windows user) instead of
  reversible obfuscation. Existing configurations are migrated on first start. A config file copied to
  another PC or user account will need its passwords entered again.

Gmail note: Google no longer accepts your normal account password from mail programs. Turn on
2-Step Verification, create an App Password (Google Account > Security > App passwords) and use
that 16 character password in the wrapper.

Outlook.com, Hotmail, Live and Microsoft 365 accounts
-----------------------------------------------------

Microsoft no longer accepts passwords from mail programs, so these accounts use Sign in with
Microsoft (OAuth 2.0). In the account wizard the password box is replaced by a button; the sign-in
happens in your web browser and the wrapper keeps a token (encrypted with DPAPI) instead of a
password. The pollers and the sender then authenticate with XOAUTH2.

For this to work the wrapper has to be registered as an application with Microsoft. That is a
one-time, free step done by whoever builds or distributes the wrapper:

1. Go to https://portal.azure.com, open Microsoft Entra ID > App registrations > New registration.
2. Name: Age of Wonders Email Wrapper. Supported account types: choose the option that includes
   personal Microsoft accounts (for example "Personal Microsoft accounts only").
3. Redirect URI: platform "Mobile and desktop applications", value `http://localhost`.
4. After registering, open Authentication and set "Allow public client flows" to Yes.
5. API permissions can be left alone: the wrapper asks for IMAP.AccessAsUser.All and SMTP.Send at
   sign-in and the user approves them on the Microsoft consent screen. To pre-list them anyway, use
   Add a permission > the "APIs my organization uses" tab > search "Office 365 Exchange Online" >
   Delegated permissions. That API is not in the default "Microsoft APIs" tab and may be absent in a
   tenant created for a personal account.
6. Copy the Application (client) ID from the Overview page and paste it into
   `AowEmailWrapper.dll.config` next to the executable:

       <add key="Microsoft.OAuth.ClientId" value="00000000-0000-0000-0000-000000000000"/>

Without a client ID the Sign in with Microsoft button explains that the feature is not set up.
The wrapper signs in at the `/consumers` endpoint, which is what a registration limited to personal
accounts requires. If you registered for "any organizational directory and personal accounts", set
`Microsoft.OAuth.Authority` in the same config file to `https://login.microsoftonline.com/common`.
Gmail could use the same mechanism, but Google requires a paid security review for the mail scope
before an app may serve the public, so Gmail stays on App Passwords for now.

Building
--------

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

    dotnet build Solution/AowEmailWrapper.sln -c Release

The executable is written to `Projects/AowEmailWrapper/bin/Release/net8.0-windows/AowEmailWrapper.exe`.

Documentation
-------------

The Quick Start Guide and the manual are HTML pages in `Docs/` (`QuickStart.html`, `Manual.html`)
with screenshots in `Docs/images/`. They are installed with the program and linked from the Start
Menu. The 2013 PDF manuals are kept in `Docs/legacy/` for reference only; their provider settings
and first-poll advice no longer apply.

Running the tests
-----------------

`Tests/AowEmailWrapper.Tests` is an xUnit project covering the local SMTP relay and MIME handling,
password storage, autoconfig parsing, provider hints, connection tests, the embedded image lists and
the message store logic. It runs against a throwaway AppData folder and never touches real settings:

    dotnet test Solution/AowEmailWrapper.sln -c Release

Two live tests are skipped by default. With `AOW_LIVE_TESTS=1` in the environment they sign in to
every password based account configured in the current user's `%APPDATA%\AowEmailWrapper` and time
an inbox scan of the startup account. The GitHub Actions workflow in `.github/workflows` builds the
solution, runs the tests and produces the installer on every push.

Building the installer
----------------------

The installer is an [Inno Setup](https://jrsoftware.org/isinfo.php) script in `Installer/`. With
the .NET 8 SDK and Inno Setup 6 installed (`winget install JRSoftware.InnoSetup`), run:

    powershell -ExecutionPolicy Bypass -File Installer/build-installer.ps1

That publishes the app (framework-dependent, 64-bit) and writes
`publish/AowEmailWrapper-<version>-setup.exe`, about 5 MB. The setup installs per user without an
administrator prompt, creates Start Menu shortcuts for the Wrapper, the Quick Start Guide, the manual
and an uninstaller, and registers in Apps and Features. If the .NET 8 Desktop Runtime is missing it
downloads the official installer from Microsoft (about 55 MB) and installs it first.

Uninstalling removes the Windows autostart entry, clears the email settings the Wrapper wrote into
the games, and asks whether to delete the settings folder under AppData.

Automatic updates
-----------------

Every push to `master` becomes an installable build. The GitHub Actions workflow builds the
installer and `Installer/publish-release.ps1` publishes it as a pre-release tagged
`v<version>-build.<run number>` for that commit, keeping the ten newest build releases. The
executable is stamped at build time with the git commit it came from (the `AowStampCommit` target
in the project file puts it in the assembly's informational version, plus the commit date).

Every start, the Wrapper asks the GitHub API for the newest release of the repository named by
`Update.Repository` in `AowEmailWrapper.dll.config`. A release counts as newer when it was built
from a different commit and published after the running build's commit, so a local build that is
ahead of the last CI build is left alone.

With *Install updates automatically* on (the default) the check runs a few seconds after start-up,
before any turn is in flight. A newer build is downloaded to `%APPDATA%\AowEmailWrapper\Updates`,
a balloon names it, the Wrapper closes, the setup runs silently (`/SILENT /NORESTART /RELAUNCH=1`,
no administrator prompt since the install is per user) and the Wrapper starts again. With the
preference off the check waits twenty seconds, shows one balloon per new build (the last announced
tag is kept in the Updates folder) and turns the Settings tab's *Check for updates* button into
*Update available*; clicking it shows the build and its notes and installs on request. The
button always works and reports "latest build" or the error when the check fails.

Only installers served from `https://github.com/<Update.Repository>/releases/download/` are ever
run. A fork should point `Update.Repository` at its own repository, or leave the value empty to
switch the feature off. Versioned releases published by hand (tags without `-build.`) are picked
up the same way and are never deleted by the pruning step.

Several copies of a game (mods)
-------------------------------

Detection no longer depends on the `Triumph Studios` registry key. `Games/GameDetector.cs` collects
candidate folders from the games' registry values (HKCU and HKLM, both views), every Steam library
listed in `libraryfolders.vdf` plus the app manifests, GOG's registry, the Apps and Features
uninstall entries, every folder under each Steam library's `common` folder, and a walk of the fixed
drives (three levels, deeper inside anything named like the game, capped at 60,000 folders), keeping
every folder that actually contains a game executable. The drive walk is the slow part (tens of seconds on
large or slow drives), so it runs on a background task only when config remembers no copies yet,
saving its result to config when done, and on Rescan; a normal start re-checks the remembered
folders (each entry stores how it was found) plus the cheap sources. Nothing is written back to the
registry during detection. The Games tab lists the result, lets the player add folders by hand,
label each copy and choose a default per game; that is stored in the `<games>` element of
config.xml and merged with detection on every start.

Mods live in separate copies of a game folder and their save files are indistinguishable, so the
copy's label is what tells them apart. When a turn is sent, `AowGameManager.ResolveOutgoing` picks
the copy whose game is running, else the copy the game was last seen in (activity log), else the
only copy holding an earlier turn, else the default; its label is written to the email as an
`X-AowEmailWrapper-Mod` header. On receipt `ResolveIncoming` uses the header label first, then the
same fallbacks. The activity log records the copy per game and the Activity Log's *Move to* fixes a
first turn that landed wrongly. All copies of one game share the registry email settings, so the
Wrapper writes them once per game.
