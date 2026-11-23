using Loadout.Core.Windows;

namespace Loadout.Core.Startup;

/// <summary>
/// Moves the "start at login" entry of the old name (Startup Profiles) to the new one, so the app never
/// starts twice and the old entry does not point at a deleted exe.
/// </summary>
public static class LegacyAutostart
{
    /// <summary>
    /// Removes the old entry. When it was switched on, switches the new one on with <paramref name="commandLine"/>;
    /// when the user had switched it off (Task Manager), leaves the new one off too. Returns true when it
    /// enabled the new entry.
    /// </summary>
    public static bool Replace(IStartupRegistration legacy, IStartupRegistration current, string commandLine)
    {
        var wasOn = legacy.IsEnabled();
        legacy.Disable();
        if (!wasOn) return false;

        current.Enable(commandLine);
        return true;
    }
}
