namespace Loadout.Core.Storage;

/// <summary>
/// The outcome of one <see cref="DataMigration.Run"/>: the error text when it failed, and the files that
/// the old app wrote to its folder after the copy, which were saved under the new folder instead of lost.
/// </summary>
public sealed record DataMigrationResult(
    DataMigrationOutcome Outcome,
    string? Error = null,
    IReadOnlyList<string>? RecoveredFiles = null);
