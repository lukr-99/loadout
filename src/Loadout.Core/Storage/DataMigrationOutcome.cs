namespace Loadout.Core.Storage;

/// <summary>What <see cref="DataMigration.Run"/> did.</summary>
public enum DataMigrationOutcome
{
    /// <summary>There was no old folder, so there was nothing to move (a new user).</summary>
    NothingToMigrate,

    /// <summary>The new folder already holds data, so it was left alone and the old one was not read.</summary>
    AlreadyPresent,

    /// <summary>The old folder was copied to the new one and verified.</summary>
    Migrated,

    /// <summary>The copy failed. The new folder was not created and the old data is untouched.</summary>
    Failed,
}
