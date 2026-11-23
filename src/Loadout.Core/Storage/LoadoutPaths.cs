namespace Loadout.Core.Storage;

/// <summary>
/// Resolves on-disk locations for Loadout's local data under %APPDATA%\Loadout. The app was called
/// Startup Profiles before, and kept its data in %APPDATA%\StartupProfiles; <see cref="DataMigration"/>
/// copies that over on first start.
/// </summary>
public static class LoadoutPaths
{
    private static string? _directoryOverride;

    /// <summary>%APPDATA%\Loadout (the folder is not created here).</summary>
    public static string NewDataDirectory => Path.Combine(AppDataRoot, "Loadout");

    /// <summary>%APPDATA%\StartupProfiles, where the app kept its data before the rename (a transition path).</summary>
    public static string LegacyDataDirectory => Path.Combine(AppDataRoot, "StartupProfiles");

    /// <summary>The folder the app reads and writes (created on first access).</summary>
    public static string DataDirectory
    {
        get
        {
            var dir = _directoryOverride ?? NewDataDirectory;
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Makes the app use the old folder for this run. Used only when the data migration failed, so the
    /// user keeps seeing the profiles and the next start can retry the move.
    /// </summary>
    public static void UseLegacyDirectoryForThisRun() => _directoryOverride = LegacyDataDirectory;

    public static string ProfilesFile => Path.Combine(DataDirectory, "profiles.json");
    public static string ConfigFile => Path.Combine(DataDirectory, "config.json");
    public static string HistoryFile => Path.Combine(DataDirectory, "history.json");

    /// <summary>The base actions that run before every profile that includes them.</summary>
    public static string BaseFile => Path.Combine(DataDirectory, "base.json");

    /// <summary>The global library of startable items that profile and base actions link to.</summary>
    public static string LibraryFile => Path.Combine(DataDirectory, "library.json");

    /// <summary>The Windows startup entries Loadout switched off at install, restored on uninstall.</summary>
    public static string StartupTakeoverFile => Path.Combine(DataDirectory, "startup-takeover.json");

    /// <summary>The Windows startup entries already looked at, so a newly added one can be offered once.</summary>
    public static string StartupSeenFile => Path.Combine(DataDirectory, "startup-seen.json");

    /// <summary>Discovery file the running host writes so agents and the tray can find its URL.</summary>
    public static string EndpointFile => Path.Combine(DataDirectory, "endpoint.json");

    /// <summary>
    /// Every place the discovery file is written: the data folder and, while the old folder exists, the old
    /// one too, so agents and skills that still read the old path keep working. Deprecated; see docs/API.md.
    /// </summary>
    public static IReadOnlyList<string> EndpointFilesToWrite()
    {
        var files = new List<string> { EndpointFile };
        var legacy = Path.Combine(LegacyDataDirectory, "endpoint.json");
        if (Directory.Exists(LegacyDataDirectory) &&
            !string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(EndpointFile), StringComparison.OrdinalIgnoreCase))
            files.Add(legacy);
        return files;
    }

    /// <summary>The discovery files to read, in order: the new path first, then the deprecated one.</summary>
    public static IReadOnlyList<string> EndpointFilesToRead() =>
        [EndpointFile, Path.Combine(LegacyDataDirectory, "endpoint.json")];

    private static string AppDataRoot => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
}
