namespace StartupProfiles.App.About;

/// <summary>
/// Everything the About page says, in one place: the author, the links, the facts, and the credits.
/// Pure data, so it is unit-tested; <see cref="AboutWindow"/> only draws it.
/// </summary>
public static class AboutInfo
{
    public const string AppName = "Startup Profiles";

    public const string Tagline = "Start what fits what you are about to do.";

    public const string Summary =
        "A small Windows launcher that runs at login. Pick a profile and it starts only the apps, " +
        "links, scripts and services that belong to it.";

    public const string AuthorName = "Lukáš Krejčí";

    public const string AuthorCredit = "Made by " + AuthorName;

    public const string FeedbackInvitation = "Feedback is welcome: what works, what does not, what is missing.";

    public const string LicenceName = "PolyForm Noncommercial 1.0.0";

    public const string LicenceUrl = "https://polyformproject.org/licenses/noncommercial/1.0.0";

    public static readonly AboutLink Instagram =
        new("Instagram", "@lukas9krejci", "https://www.instagram.com/lukas9krejci/");

    public static readonly AboutLink GitHub =
        new("GitHub", "lukr-99", "https://github.com/lukr-99");

    public static readonly AboutLink Issues =
        new("Issues", "Report or suggest", "https://github.com/lukr-99/startup-profiles/issues");

    public static IReadOnlyList<AboutLink> Links { get; } = [Instagram, GitHub, Issues];

    /// <summary>What ships with the app besides its own code. Fonts and icons come from Windows, not from the app.</summary>
    public static IReadOnlyList<AboutCredit> Credits { get; } =
    [
        new("DotNetLib.Core and DotNetLib.Tray", "Same author, no licence declared", "MVVM helpers, self-update, tray kit, theme engine"),
        new("H.NotifyIcon.Wpf", "MIT", "The notification area icon"),
        new("WPF-UI", "MIT", "Themes for the tray menu"),
        new("Segoe UI and Segoe Fluent Icons", "Part of Windows, not shipped", "Text and the launcher tile icons"),
    ];

    /// <summary>"0.2.0" from an assembly version; "unknown" when the build did not stamp one.</summary>
    public static string FormatVersion(Version? version) =>
        version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";

    /// <summary>The running app's version, from the build's single source of truth (VersionPrefix).</summary>
    public static string CurrentVersion => FormatVersion(typeof(AboutInfo).Assembly.GetName().Version);

    public static IReadOnlyList<AboutFact> Facts(string version) =>
    [
        new("Version", version),
        new("Licence", LicenceName),
        new("Platform", "Windows, .NET 10"),
        new("Source", "github.com/lukr-99/startup-profiles"),
    ];
}
