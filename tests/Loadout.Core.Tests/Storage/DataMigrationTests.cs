using Loadout.Core.Storage;

namespace Loadout.Core.Tests.Storage;

public sealed class DataMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "loadout-migration-" + Guid.NewGuid().ToString("N"));
    private string Legacy => Path.Combine(_root, "StartupProfiles");
    private string Target => Path.Combine(_root, "Loadout");

    public DataMigrationTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private DataMigration Create() => new(Legacy, Target);

    private void WriteLegacy(string relative, string content)
    {
        var path = Path.Combine(Legacy, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void New_user_has_nothing_to_migrate_and_no_folder_is_created()
    {
        var result = Create().Run();

        Assert.Equal(DataMigrationOutcome.NothingToMigrate, result.Outcome);
        Assert.False(Directory.Exists(Target));
        Assert.False(Directory.Exists(Legacy));
    }

    [Fact]
    public void Migrating_user_gets_every_file_and_the_old_folder_is_kept_with_a_note()
    {
        WriteLegacy("profiles.json", "[{\"id\":\"work\"}]");
        WriteLegacy("library.json", "{\"items\":[1,2]}");
        WriteLegacy("backups/old.json", "nested");
        WriteLegacy("endpoint.json", "{\"url\":\"http://127.0.0.1:1\"}");

        var result = Create().Run();

        Assert.Equal(DataMigrationOutcome.Migrated, result.Outcome);
        Assert.Equal("[{\"id\":\"work\"}]", File.ReadAllText(Path.Combine(Target, "profiles.json")));
        Assert.Equal("{\"items\":[1,2]}", File.ReadAllText(Path.Combine(Target, "library.json")));
        Assert.Equal("nested", File.ReadAllText(Path.Combine(Target, "backups", "old.json")));
        Assert.False(File.Exists(Path.Combine(Target, "endpoint.json"))); // a stale port is not copied
        Assert.False(Directory.Exists(Target + ".migrating"));

        // Nothing was removed from the old folder, and it now has the note.
        Assert.Equal("[{\"id\":\"work\"}]", File.ReadAllText(Path.Combine(Legacy, "profiles.json")));
        Assert.True(File.Exists(Path.Combine(Legacy, DataMigration.NoteFileName)));
        Assert.False(File.Exists(Path.Combine(Target, DataMigration.NoteFileName)));
    }

    [Fact]
    public void Both_folders_present_leaves_both_untouched()
    {
        WriteLegacy("profiles.json", "old");
        Directory.CreateDirectory(Target);
        File.WriteAllText(Path.Combine(Target, "profiles.json"), "new");

        var result = Create().Run();

        Assert.Equal(DataMigrationOutcome.AlreadyPresent, result.Outcome);
        Assert.Equal("new", File.ReadAllText(Path.Combine(Target, "profiles.json")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Legacy, "profiles.json")));
        Assert.False(File.Exists(Path.Combine(Legacy, DataMigration.NoteFileName)));
    }

    [Fact]
    public void An_empty_new_folder_does_not_block_the_migration()
    {
        WriteLegacy("profiles.json", "old");
        Directory.CreateDirectory(Target);

        var result = Create().Run();

        Assert.Equal(DataMigrationOutcome.Migrated, result.Outcome);
        Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "profiles.json")));
    }

    [Fact]
    public void A_failure_half_way_loses_nothing_leaves_no_new_folder_and_a_retry_works()
    {
        WriteLegacy("a-profiles.json", "first");
        WriteLegacy("b-library.json", "second");

        // Lock the second file so copying it fails after the first one was already copied.
        FileStream? held = new(Path.Combine(Legacy, "b-library.json"), FileMode.Open, FileAccess.Read, FileShare.None);
        DataMigrationResult failed;
        try { failed = Create().Run(); }
        finally { held.Dispose(); }

        Assert.Equal(DataMigrationOutcome.Failed, failed.Outcome);
        Assert.False(string.IsNullOrEmpty(failed.Error));
        Assert.False(Directory.Exists(Target));                    // the app never sees a half-filled folder
        Assert.False(Directory.Exists(Target + ".migrating"));
        Assert.Equal("first", File.ReadAllText(Path.Combine(Legacy, "a-profiles.json")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(Legacy, "b-library.json")));
        Assert.False(File.Exists(Path.Combine(Legacy, DataMigration.NoteFileName)));

        var retry = Create().Run();

        Assert.Equal(DataMigrationOutcome.Migrated, retry.Outcome);
        Assert.Equal("first", File.ReadAllText(Path.Combine(Target, "a-profiles.json")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(Target, "b-library.json")));
    }

    [Fact]
    public void A_staging_folder_left_by_a_crash_is_cleared_before_the_retry()
    {
        WriteLegacy("profiles.json", "real");
        Directory.CreateDirectory(Target + ".migrating");
        File.WriteAllText(Path.Combine(Target + ".migrating", "junk.json"), "junk");

        var result = Create().Run();

        Assert.Equal(DataMigrationOutcome.Migrated, result.Outcome);
        Assert.False(File.Exists(Path.Combine(Target, "junk.json")));
        Assert.Equal("real", File.ReadAllText(Path.Combine(Target, "profiles.json")));
    }

    [Fact]
    public void Running_again_after_a_good_migration_does_nothing()
    {
        WriteLegacy("profiles.json", "old");
        Assert.Equal(DataMigrationOutcome.Migrated, Create().Run().Outcome);

        Assert.Equal(DataMigrationOutcome.AlreadyPresent, Create().Run().Outcome);
    }
}
