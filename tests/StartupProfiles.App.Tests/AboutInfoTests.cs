using StartupProfiles.App.About;
using StartupProfiles.App.Tray;
using StartupProfiles.App.Themes;

namespace StartupProfiles.App.Tests;

public sealed class AboutInfoTests
{
    [Fact]
    public void TheAuthorCredit_NamesTheAuthor() =>
        Assert.Equal("Made by Lukáš Krejčí", AboutInfo.AuthorCredit);

    [Fact]
    public void TheLinks_AreInstagramGitHubAndTheIssuesPage() =>
        Assert.Equal(
            ["https://www.instagram.com/lukas9krejci/", "https://github.com/lukr-99", "https://github.com/lukr-99/startup-profiles/issues"],
            AboutInfo.Links.Select(l => l.Url));

    [Fact]
    public void EveryLink_IsAnAbsoluteHttpsUrl_WithALabelAndHandle() =>
        Assert.All(AboutInfo.Links, l =>
        {
            Assert.True(Uri.TryCreate(l.Url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
            Assert.False(string.IsNullOrWhiteSpace(l.Label));
            Assert.False(string.IsNullOrWhiteSpace(l.Handle));
        });

    [Theory]
    [InlineData(0, 2, 0, "0.2.0")]
    [InlineData(1, 10, 3, "1.10.3")]
    public void FormatVersion_DropsTheRevision(int major, int minor, int build, string expected) =>
        Assert.Equal(expected, AboutInfo.FormatVersion(new Version(major, minor, build, 0)));

    [Fact]
    public void FormatVersion_WithoutAVersion_SaysUnknown() =>
        Assert.Equal("unknown", AboutInfo.FormatVersion(null));

    [Fact]
    public void TheCurrentVersion_IsThreeNumbers() =>
        Assert.Matches(@"^\d+\.\d+\.\d+$", AboutInfo.CurrentVersion);

    [Fact]
    public void TheFacts_StartWithTheVersion_AndNameTheLicence()
    {
        var facts = AboutInfo.Facts("9.9.9");

        Assert.Equal(new AboutFact("Version", "9.9.9"), facts[0]);
        Assert.Contains(facts, f => f.Value == AboutInfo.LicenceName);
    }

    [Fact]
    public void TheCredits_ListTheShippedLibraries() =>
        Assert.Contains(AboutInfo.Credits, c => c.Name == "H.NotifyIcon.Wpf" && c.Licence == "MIT");

    [Fact]
    public void TheTrayMenu_OffersAbout() =>
        Assert.Contains(TrayMenuModel.Build([], null, ThemeMode.System, "0.2.0"), e => e.Command == TrayCommand.OpenAbout);
}
