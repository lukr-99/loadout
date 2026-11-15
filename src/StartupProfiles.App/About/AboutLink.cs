namespace StartupProfiles.App.About;

/// <summary>One link button on the About page.</summary>
/// <param name="Label">The service name, for example "GitHub".</param>
/// <param name="Handle">What the button shows under the name, for example "lukr-99".</param>
/// <param name="Url">Where the button goes.</param>
public sealed record AboutLink(string Label, string Handle, string Url);
