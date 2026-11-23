using System.Security.Cryptography;

namespace Loadout.Core.Storage;

/// <summary>
/// Moves the data of the old app name (Startup Profiles, %APPDATA%\StartupProfiles) to the new folder
/// (%APPDATA%\Loadout) on first start. The old folder is never deleted or changed, apart from a
/// <c>MOVED-TO-LOADOUT.txt</c> note added after a good copy.
/// </summary>
/// <remarks>
/// The copy goes to a staging folder next to the new one, is checked file by file (size and SHA-256),
/// and only then renamed into place. The new folder therefore exists only when the copy is complete, so
/// a failure half way leaves no half-filled data folder and the next start simply tries again.
/// </remarks>
public sealed class DataMigration
{
    /// <summary>The note left in the old folder after a good copy.</summary>
    public const string NoteFileName = "MOVED-TO-LOADOUT.txt";

    private const string StagingSuffix = ".migrating";

    // The discovery file belongs to a running process; a copy of it would point at a dead port.
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase) { "endpoint.json" };

    private readonly string _legacy;
    private readonly string _target;

    public DataMigration(string legacyDirectory, string newDirectory)
    {
        _legacy = legacyDirectory;
        _target = newDirectory;
    }

    /// <summary>The migration between the app's real old and new folders.</summary>
    public static DataMigration ForCurrentUser() => new(LoadoutPaths.LegacyDataDirectory, LoadoutPaths.NewDataDirectory);

    public DataMigrationResult Run()
    {
        try
        {
            if (HasEntries(_target)) return new(DataMigrationOutcome.AlreadyPresent);
            if (!Directory.Exists(_legacy)) return new(DataMigrationOutcome.NothingToMigrate);

            var staging = _target + StagingSuffix;
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); // left by an earlier failed try

            Copy(_legacy, staging);
            Verify(_legacy, staging);

            // An empty folder may exist (something created it before we ran); move into its place.
            if (Directory.Exists(_target)) Directory.Delete(_target, recursive: false);
            Directory.Move(staging, _target);

            WriteNote();
            return new(DataMigrationOutcome.Migrated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            TryDelete(_target + StagingSuffix);
            return new(DataMigrationOutcome.Failed, ex.Message);
        }
    }

    private static bool HasEntries(string directory) =>
        Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any();

    private static void Copy(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            if (Skipped.Contains(Path.GetFileName(file))) continue;
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
            Copy(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static void Verify(string source, string copy)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (Skipped.Contains(Path.GetFileName(file))) continue;

            var other = Path.Combine(copy, relative);
            if (!File.Exists(other) || Hash(file) != Hash(other))
                throw new InvalidDataException($"The copy of '{relative}' does not match the original.");
        }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private void WriteNote()
    {
        try
        {
            File.WriteAllText(Path.Combine(_legacy, NoteFileName),
                "Loadout (the new name of Startup Profiles) copied this folder to:" + Environment.NewLine +
                _target + Environment.NewLine + Environment.NewLine +
                "Nothing here was deleted. Loadout now reads and writes the new folder. You can delete this folder" + Environment.NewLine +
                "when you no longer need the old copy. While it exists, Loadout also writes endpoint.json here so" + Environment.NewLine +
                "older agents and skills can still find the app." + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The note is a courtesy. The data is already safe in the new folder.
        }
    }

    private static void TryDelete(string directory)
    {
        try { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left behind; the next run clears it before copying.
        }
    }
}
