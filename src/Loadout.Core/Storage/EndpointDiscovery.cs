using System.Text.Json;

namespace Loadout.Core.Storage;

/// <summary>
/// Reads the discovery files (endpoint.json) in order and returns the URLs that might be alive. A file whose
/// process is gone is skipped, so a stale old file cannot hide the live one (or send a caller to a dead port).
/// </summary>
public static class EndpointDiscovery
{
    /// <param name="files">The discovery files, best first (the new path before the deprecated one).</param>
    /// <param name="isProcessAlive">Tells whether the process id written in a file still exists.</param>
    public static IReadOnlyList<string> ReadUrls(IEnumerable<string> files, Func<int, bool> isProcessAlive)
    {
        var urls = new List<string>();
        foreach (var file in files)
        {
            try
            {
                if (!File.Exists(file)) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                if (!root.TryGetProperty("url", out var url) || url.GetString() is not { Length: > 0 } text) continue;
                if (root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var id) && !isProcessAlive(id)) continue;
                if (!urls.Contains(text)) urls.Add(text);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
            {
                // Unreadable or half-written; try the next path.
            }
        }

        return urls;
    }
}
