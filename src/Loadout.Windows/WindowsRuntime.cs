using Loadout.Core.Actions;

namespace Loadout.Windows;

/// <summary>Windows composition root: builds the action registry wired to the platform adapters.</summary>
public static class WindowsRuntime
{
    public static ActionHandlerRegistry CreateActionRegistry() =>
        ActionHandlerRegistry.CreateDefault(
            new SystemProcessLauncher(),
            new WindowsServiceController(),
            new WindowsVpnConnector());
}
