using Loadout.Core.Startup;
using Loadout.Core.Storage;

namespace Loadout.Core.Tests.Startup;

/// <summary>In-memory <see cref="IStartupTakeoverStore"/> so takeover tests stay file-free.</summary>
internal sealed class FakeStartupTakeoverStore : IStartupTakeoverStore
{
    public List<StartupEntry> Entries { get; private set; } = [];

    public IReadOnlyList<StartupEntry> Load() => Entries.ToList();

    public void Save(IReadOnlyList<StartupEntry> entries) => Entries = [.. entries];

    public void Clear() => Entries = [];
}
