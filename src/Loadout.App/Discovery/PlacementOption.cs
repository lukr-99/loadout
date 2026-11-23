using Loadout.Core.Startup;

namespace Loadout.App.Discovery;

/// <summary>One answer to "where should this app start?", in plain words.</summary>
public sealed record PlacementOption(string Label, StartupPlacement Placement)
{
    // The themed combo box shows the selected item as text.
    public override string ToString() => Label;
}
