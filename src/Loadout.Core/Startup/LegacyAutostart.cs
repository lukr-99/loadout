using Loadout.Core.Windows;

namespace Loadout.Core.Startup;

/// <summary>
/// Moves the "start at login" entry of the old name (Startup Profiles) to the new one, so the app never
/// starts twice and the old entry does not point at a deleted exe. The user's choice is kept: an old entry
/// that was switched off (Task Manager) does not turn the new one on. The new entry is written first, so a
/// failure never leaves the user with no entry at all.
/// </summary>
public static class LegacyAutostart
{
    /// <summary>
    /// For the running app: when an old entry exists, replaces it. Returns true when it enabled the new entry.
    /// With no old entry it does nothing.
    /// </summary>
    public static bool Replace(IStartupRegistration legacy, IStartupRegistration current, string commandLine)
    {
        if (!legacy.HasEntry()) return false;

        var wasOn = legacy.IsEnabled();
        if (wasOn) current.Enable(commandLine);
        legacy.Disable();
        return wasOn;
    }

    /// <summary>
    /// For the installer (<c>--register-login</c>): registers the new entry, unless the old one was there and
    /// switched off. With no old entry (a first install) it registers. The old entry is removed last.
    /// </summary>
    public static bool Register(IStartupRegistration legacy, IStartupRegistration current, string commandLine)
    {
        var hadOldEntry = legacy.HasEntry();
        var enable = !hadOldEntry || legacy.IsEnabled();
        if (enable) current.Enable(commandLine);
        legacy.Disable();
        return enable;
    }
}
