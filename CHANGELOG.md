# Changelog

Every release, newest first. The version lives in `VersionPrefix` in `Directory.Build.props`, and a
release is a Git tag `vX.Y.Z` that matches it.

## [Unreleased]

- **Startup Profiles is now Loadout.** The exe is `Loadout.exe`, the install folder is
  `%LOCALAPPDATA%\Programs\Loadout`, and the installer is `Loadout-Setup-<version>.exe`. Installing
  it over Startup Profiles upgrades in place: the old program folder, Start Menu shortcut and agent
  skill are removed, and the login entry is replaced. The projects, namespaces and solution are renamed
  to `Loadout.*` too.
- **Your data moves, nothing is deleted.** On the first start, `%APPDATA%\StartupProfiles` is copied to
  `%APPDATA%\Loadout`, checked file by file, and then put in place. The old folder stays, with a
  `MOVED-TO-LOADOUT.txt` note. If the copy fails, the app keeps using the old folder for that run and
  tries again next time.
- Transition: `endpoint.json` is written to the new and the old data folder, and the agent skill (now
  `skills/loadout`) reads the new path first. The old path is deprecated. The `startupprofiles://`
  scheme still works next to the new `loadout://`. Releases also attach the installer as
  `StartupProfiles-Setup-<version>.exe` so installs of the old name can update themselves.
- New About window (tray menu, and Settings in the configuration window): who made the app, a feedback
  invitation with Instagram, GitHub and Issues links, the app's facts, and credits.
- A profile run that throws no longer leaves the launcher stuck or the configuration window on
  "Running...". The launcher stays open and shows the error, the configuration window shows
  "Run failed", the tray shows a balloon, and each failure is written to error.log.
- Warnings and errors from the app's logging now also go to `error.log`, next to the crash reports.
- Update System.ServiceProcess.ServiceController to 10.0.12 and DotNetLib to 0.4.1.
- The launcher sizes itself to the number of profiles and always sits at the bottom centre of the
  screen. A long status message is trimmed instead of widening the window.
- Launcher tiles use one Segoe Fluent Icons style, picked by the profile's id or icon key, with the
  emoji or the name's initial as a fallback.

## [0.2.0] - 2026-10-02

The first published release. Earlier builds reported 0.1.0 but were never released, so 0.2.0 is
newer than any installed copy and the in-app updater offers it. It contains:

- A login launcher that asks what you are about to do and runs only that profile's actions: apps,
  URLs, scripts, folders, services, VPN, delays, waits, and kills.
- A configuration window with Profiles and Settings tabs, emoji profile icons, and light, dark, and
  system themes. The system theme follows Windows while the app runs.
- The base: actions that run before every profile, unless a profile opts out with **Also start Base**.
- A global library of startable items. Profile rows link to library items, and a side panel offers
  the apps Windows starts at login and lets you drag them into profiles.
- Startup takeover: the installer moves the apps Windows starts at login into the Everything profile
  and switches them off in Windows. Uninstall switches them back on.
- A tray icon to switch profiles, a loopback HTTP API for agents and tools, and a bundled agent skill.
- The integration contract: other apps ask to be added through a `startupprofiles://` link, the CLI,
  or the API, and the user decides in a Startup Profiles window.
- A per-user `install/install.ps1` and `install/uninstall.ps1`, and a tag-driven release workflow.
- A profile editor that shows each step as a card in plain words, saves every change by itself, and
  removes a step with a trash icon and Undo. A grouped icon picker that works in the dark theme.
- A launcher that greets you, pre-selects the profile you used last, and, at login, can start a
  profile after a 10-second countdown you can cancel (the profile's **At login** setting).
- New startup apps: apps that add themselves to Windows startup later are offered once at launch,
  so you choose the profile they start with.
- Full backup and restore of profiles, Base, Created items, and settings, with a check and a preview
  before anything is replaced.
- A per-user installer, `StartupProfiles-Setup-<version>.exe`, attached to every release with its
  SHA-256 checksum.
- Updates: the app checks GitHub Releases once a day (or on demand in Settings and the tray), downloads
  the installer, checks its SHA-256, and installs it silently when you agree.
- The tray, theme engine, single-instance handling, and MVVM base now come from the shared DotNetLib
  packages. The theme follows Windows high contrast, and the tray menu has a Theme submenu and marks the
  active profile.
