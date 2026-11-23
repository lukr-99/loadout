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

    /// <summary>
    /// The URLs a running host may have: the new discovery file first, then the deprecated old one. A file
    /// whose process is gone is skipped.
    /// </summary>
    public static IReadOnlyList<string> ReadUrls() =>
        EndpointDiscovery.ReadUrls(LoadoutPaths.EndpointFilesToRead(), IsProcessAlive);

    private static bool IsProcessAlive(int id)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
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
