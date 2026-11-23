using System.Security.Cryptography;
using System.Text;

namespace Loadout.Core.Storage;

/// <summary>
/// Moves the data of the old app name (Startup Profiles, %APPDATA%\StartupProfiles) to the new folder
/// (%APPDATA%\Loadout) on first start. The old folder is never deleted or changed, apart from a
/// <c>MOVED-TO-LOADOUT.txt</c> note added after a good copy.
/// </summary>
/// <remarks>
/// The copy goes to a staging folder next to the new one, is checked file by file (size and SHA-256),
/// and only then renamed into place. The new folder therefore holds data only when the copy is complete,
/// so a failure half way leaves no half-filled data folder and the next start simply tries again.
/// A named mutex keeps two processes from migrating at once. While the old app still runs, the copy is
/// deferred, because it could still write to the old folder. Anything the old app writes there after
/// the copy is saved under <c>recovered-from-startupprofiles</c> in the new folder, never dropped.
/// </remarks>
public sealed class DataMigration
{
    /// <summary>The note left in the old folder after a good copy.</summary>
    public const string NoteFileName = "MOVED-TO-LOADOUT.txt";

    /// <summary>Folder in the new data folder that receives files the old app wrote after the copy.</summary>
    public const string RecoveredFolderName = "recovered-from-startupprofiles";

    private const string StagingSuffix = ".migrating";
    private const string SidelineSuffix = ".before-migration";

    // The discovery file belongs to a running process; a copy of it would point at a dead port.
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase) { "endpoint.json" };

    // The files that make a folder "the app's data". Stray files (error.log, endpoint.json, a config that only
    // holds the API port) do not, so they cannot make a new folder look like a finished setup.
    private static readonly string[] MarkerFiles = ["profiles.json", "library.json", "base.json"];

    private static readonly string[] DataFiles =
        ["profiles.json", "library.json", "base.json", "config.json", "history.json", "startup-takeover.json", "startup-seen.json"];

    private readonly string _legacy;
    private readonly string _target;
    private readonly Func<bool>? _isLegacyAppRunning;
    private readonly TimeSpan _lockTimeout;

    public DataMigration(
        string legacyDirectory, string newDirectory, Func<bool>? isLegacyAppRunning = null, TimeSpan? lockTimeout = null)
    {
        _legacy = legacyDirectory;
        _target = newDirectory;
        _isLegacyAppRunning = isLegacyAppRunning;
        _lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>The migration between the app's real old and new folders.</summary>
    /// <param name="isLegacyAppRunning">Tells whether the old app (Startup Profiles) is running; the copy waits for it to exit.</param>
    public static DataMigration ForCurrentUser(Func<bool>? isLegacyAppRunning = null) =>
        new(LoadoutPaths.LegacyDataDirectory, LoadoutPaths.NewDataDirectory, isLegacyAppRunning);

    public DataMigrationResult Run()
    {
        using var gate = new Mutex(false, GateName());
        var held = false;
        try
        {
            try { held = gate.WaitOne(_lockTimeout); }
            catch (AbandonedMutexException) { held = true; } // another process died inside the lock; its staging is cleared below

            if (!held) return new(DataMigrationOutcome.Failed, "Another process is still moving the data.");
            return RunLocked();
        }
        catch (Exception ex)
        {
            // Whatever went wrong, report it instead of crashing, and never leave a half-made folder behind.
            TryDelete(_target + StagingSuffix);
            return new(DataMigrationOutcome.Failed, ex.Message);
        }
        finally
        {
            if (held) gate.ReleaseMutex();
        }
    }

    private DataMigrationResult RunLocked()
    {
        if (HasData(_target)) return new(DataMigrationOutcome.AlreadyPresent, RecoveredFiles: RecoverLateWrites());
        if (!Directory.Exists(_legacy)) return new(DataMigrationOutcome.NothingToMigrate);
        if (_isLegacyAppRunning?.Invoke() == true) return new(DataMigrationOutcome.Deferred);

        var staging = _target + StagingSuffix;
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); // left by an earlier failed try

        Copy(_legacy, staging);
        Verify(_legacy, staging);

        SidelineTarget();
        Directory.Move(staging, _target);

        WriteNote();
        return new(DataMigrationOutcome.Migrated);
    }

    private static bool HasData(string directory) =>
        Directory.Exists(directory) && MarkerFiles.Any(name => File.Exists(Path.Combine(directory, name)));

    // A new folder with only stray files (no profiles or library) is moved aside, not deleted.
    private void SidelineTarget()
    {
        if (!Directory.Exists(_target)) return;
        if (!Directory.EnumerateFileSystemEntries(_target).Any())
        {
            Directory.Delete(_target, recursive: false);
            return;
        }

        var aside = _target + SidelineSuffix;
        if (Directory.Exists(aside)) aside += "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        Directory.Move(_target, aside);
    }

    // The old app may have written to its folder after the copy (for example it was still running). Keep those
    // files next to the new data instead of losing them, and move the note's time on so it happens once.
    private List<string>? RecoverLateWrites()
    {
        try
        {
            var note = Path.Combine(_legacy, NoteFileName);
            if (!File.Exists(note)) return null;

            var since = File.GetLastWriteTimeUtc(note) + TimeSpan.FromSeconds(1);
            var newest = DateTime.MinValue;
            var recovered = new List<string>();
            string? folder = null;

            foreach (var name in DataFiles)
            {
                var source = Path.Combine(_legacy, name);
                if (!File.Exists(source) || File.GetLastWriteTimeUtc(source) <= since) continue;

                var current = Path.Combine(_target, name);
                if (File.Exists(current) && Hash(current) == Hash(source)) continue; // the same content, nothing to keep

                folder ??= Path.Combine(_target, RecoveredFolderName, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
                Directory.CreateDirectory(folder);
                File.Copy(source, Path.Combine(folder, name), overwrite: true);
                recovered.Add(name);
                var written = File.GetLastWriteTimeUtc(source);
                if (written > newest) newest = written;
            }

            if (recovered.Count == 0) return null;
            File.SetLastWriteTimeUtc(note, newest > DateTime.UtcNow ? newest : DateTime.UtcNow);
            return recovered;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null; // best effort; the old folder is still intact
        }
    }

    private string GateName()
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(_target).ToLowerInvariant())));
        return @"Local\Loadout.DataMigration." + key[..16];
    }

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
                "older agents and skills can still find the app. Files the old app writes here after the copy are" + Environment.NewLine +
                "saved under " + RecoveredFolderName + " in the new folder." + Environment.NewLine);
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
