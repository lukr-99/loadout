namespace Loadout.App.Tray;

/// <summary>What a tray menu item does. <see cref="TrayIcon"/> maps each to the app's action.</summary>
public enum TrayCommand
{
    None,
    OpenLauncher,
    RunProfile,
    OpenConfiguration,
    SetTheme,
    CheckForUpdates,
    InstallUpdate,
    OpenDataFolder,
    OpenAbout,
    Exit,
}
