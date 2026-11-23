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

    [Fact]
    public void Stray_files_in_the_new_folder_do_not_count_as_data_and_are_kept_aside()
    {
        WriteLegacy("profiles.json", "real");
        Directory.CreateDirectory(Target);
        File.WriteAllText(Path.Combine(Target, "error.log"), "boom");
        File.WriteAllText(Path.Combine(Target, "config.json"), "{}");

        var result = Create().Run();

        Assert.Equal(DataMigrationOutcome.Migrated, result.Outcome);
        Assert.Equal("real", File.ReadAllText(Path.Combine(Target, "profiles.json")));
        var aside = Target + ".before-migration";
        Assert.Equal("boom", File.ReadAllText(Path.Combine(aside, "error.log"))); // nothing is thrown away
    }

    [Fact]
    public void A_new_folder_with_only_stray_files_and_no_old_folder_stays_as_it_is()
    {
        Directory.CreateDirectory(Target);
        File.WriteAllText(Path.Combine(Target, "error.log"), "boom");

        var result = Create().Run();

        Assert.Equal(DataMigrationOutcome.NothingToMigrate, result.Outcome);
        Assert.True(File.Exists(Path.Combine(Target, "error.log")));
    }

    [Fact]
    public void Two_runs_at_the_same_time_migrate_once_and_neither_breaks_the_other()
    {
        for (var i = 0; i < 20; i++) WriteLegacy($"extra-{i}.json", new string('x', 50_000));
        WriteLegacy("profiles.json", "real");

        var results = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => Create().Run()))
            .ToArray()
            .Select(t => t.GetAwaiter().GetResult())
            .ToList();

        Assert.Equal(1, results.Count(r => r.Outcome == DataMigrationOutcome.Migrated));
        Assert.Equal(3, results.Count(r => r.Outcome == DataMigrationOutcome.AlreadyPresent));
        Assert.Equal("real", File.ReadAllText(Path.Combine(Target, "profiles.json")));
        Assert.Equal(21, Directory.GetFiles(Target).Length);
        Assert.False(Directory.Exists(Target + ".migrating"));
    }

    [Fact]
    public void While_the_old_app_runs_nothing_is_copied_and_a_later_run_migrates()
    {
        WriteLegacy("profiles.json", "old");
        var running = true;
        DataMigration Deferring() => new(Legacy, Target, () => running);

        var first = Deferring().Run();

        Assert.Equal(DataMigrationOutcome.Deferred, first.Outcome);
        Assert.False(Directory.Exists(Target));
        Assert.False(File.Exists(Path.Combine(Legacy, DataMigration.NoteFileName)));

        running = false;
        var second = Deferring().Run();

        Assert.Equal(DataMigrationOutcome.Migrated, second.Outcome);
        Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "profiles.json")));
    }

    [Fact]
    public void The_running_check_is_not_asked_when_there_is_nothing_to_move()
    {
        var asked = false;

        var result = new DataMigration(Legacy, Target, () => { asked = true; return true; }).Run();

        Assert.Equal(DataMigrationOutcome.NothingToMigrate, result.Outcome);
        Assert.False(asked);
    }

    [Fact]
    public void A_file_the_old_app_writes_after_the_copy_is_saved_not_lost_and_reported_once()
    {
        WriteLegacy("profiles.json", "at copy time");
        WriteLegacy("library.json", "unchanged");
        Assert.Equal(DataMigrationOutcome.Migrated, Create().Run().Outcome);

        var later = Path.Combine(Legacy, "profiles.json");
        File.WriteAllText(later, "written by the old app afterwards");
        File.SetLastWriteTimeUtc(later, DateTime.UtcNow.AddMinutes(5));

        var result = Create().Run();

        Assert.Equal(DataMigrationOutcome.AlreadyPresent, result.Outcome);
        Assert.Equal(["profiles.json"], result.RecoveredFiles);
        Assert.Equal("at copy time", File.ReadAllText(Path.Combine(Target, "profiles.json"))); // new data is not overwritten
        var saved = Directory.GetFiles(Path.Combine(Target, DataMigration.RecoveredFolderName), "profiles.json", SearchOption.AllDirectories);
        Assert.Equal("written by the old app afterwards", File.ReadAllText(Assert.Single(saved)));

        Assert.Null(Create().Run().RecoveredFiles); // already saved, not repeated on every start
    }
}
