using System.IO;
using System.Text.Json;
using Loadout.Core.Storage;

namespace Loadout.App;

/// <summary>
/// Publishes the discovery file (endpoint.json) so agents and the tray can find the running instance's
/// URL without guessing the port. It is written to the data folder and, while the old
/// %APPDATA%\StartupProfiles folder exists, to that one too (deprecated, so older agents keep working).
/// Best-effort: failures never take down the host.
/// </summary>
internal static class RuntimeInfo
{
    public static void Write(int port)
    {
        var info = new
        {
            url = $"http://127.0.0.1:{port}",
            port,
            pid = Environment.ProcessId,
            version = typeof(RuntimeInfo).Assembly.GetName().Version?.ToString(),
            startedAt = DateTimeOffset.UtcNow,
        };
        var json = JsonSerializer.Serialize(info);
        foreach (var file in LoadoutPaths.EndpointFilesToWrite())
        {
            try { File.WriteAllText(file, json); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* best effort */ }
        }
    }

    /// <summary>The running host's URL: the new discovery file first, then the deprecated old one.</summary>
    public static string? ReadUrl()
    {
        foreach (var file in LoadoutPaths.EndpointFilesToRead())
        {
            try
            {
                if (!File.Exists(file)) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.GetProperty("url").GetString() is { } url) return url;
            }
            catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or UnauthorizedAccessException)
            {
                // Try the next path.
            }
        }

        return null;
    }

    public static void Delete()
    {
        foreach (var file in LoadoutPaths.EndpointFilesToWrite())
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* ignore */ }
        }
    }
}
