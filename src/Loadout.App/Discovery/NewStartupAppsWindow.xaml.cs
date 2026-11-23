using System.Windows;
using Loadout.App.Themes;

namespace Loadout.App.Discovery;

/// <summary>Offers the startup apps found since last time (see <see cref="NewStartupAppsViewModel"/>).</summary>
public partial class NewStartupAppsWindow : Window
{
    public NewStartupAppsWindow(NewStartupAppsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += () => Dispatcher.BeginInvoke(Close);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeManager.ApplyTitleBar(this);
    }
}
