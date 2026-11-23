using Loadout.Core.Actions;

namespace Loadout.Core.Windows;

/// <summary>Connects a named VPN entry. Implemented by a platform adapter in Loadout.Windows.</summary>
public interface IVpnConnector
{
    /// <summary>Connects the named VPN entry using its saved credentials.</summary>
    ActionResult Connect(string connectionName);
}
