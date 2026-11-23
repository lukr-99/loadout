namespace Loadout.Core.Windows;

/// <summary>
/// Registers (or unregisters) the launcher to run at login. The seam is portable; the Windows adapter
/// lives in Loadout.Windows so Core stays platform-independent and testable.
/// </summary>
public interface IStartupRegistration
{
    /// <summary>True if a login entry exists, even one the user switched off in Task Manager.</summary>
    bool HasEntry();

    /// <summary>True if the launcher is currently set to run at login.</summary>
    bool IsEnabled();

    /// <summary>Registers <paramref name="commandLine"/> to run at login (idempotent).</summary>
    void Enable(string commandLine);

    /// <summary>Removes the login registration if present (idempotent).</summary>
    void Disable();
}
