using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Loadout.App.Themes;

namespace Loadout.App.About;

/// <summary>The About page: the app, who made it, how to send feedback, and what it is built on.</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        NameText.Text = AboutInfo.AppName;
        TaglineText.Text = AboutInfo.Tagline;
        SummaryText.Text = AboutInfo.Summary;
        AuthorText.Text = AboutInfo.AuthorCredit;
        FeedbackText.Text = AboutInfo.FeedbackInvitation;
        LicenceLink.NavigateUri = new Uri(AboutInfo.LicenceUrl);
        VersionText.Text = $"Version {AboutInfo.CurrentVersion}";
        LinkList.ItemsSource = AboutInfo.Links;
        FactList.ItemsSource = AboutInfo.Facts(AboutInfo.CurrentVersion);
        CreditList.ItemsSource = AboutInfo.Credits;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeManager.ApplyTitleBar(this);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        // A soft fade-in, skipped when Windows has animations turned off.
        if (!SystemParameters.ClientAreaAnimation) return;
        Root.Opacity = 0;
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
    }

    private void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url }) Open(url);
    }

    private void OnLicenceClick(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Open(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Win32Exception) { /* No browser registered; the link text is still visible. */ }
    }
}
