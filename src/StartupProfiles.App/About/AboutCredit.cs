namespace StartupProfiles.App.About;

/// <summary>One line in the credits: something the app ships or is built on.</summary>
/// <param name="Name">The library, font, or icon set.</param>
/// <param name="Licence">Its licence, or a short note when it has none declared.</param>
/// <param name="Role">What the app uses it for.</param>
public sealed record AboutCredit(string Name, string Licence, string Role);
