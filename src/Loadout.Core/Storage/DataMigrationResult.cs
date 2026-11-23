namespace Loadout.Core.Storage;

/// <summary>The outcome of one <see cref="DataMigration.Run"/>, with the error text when it failed.</summary>
public sealed record DataMigrationResult(DataMigrationOutcome Outcome, string? Error = null);
