using System.IO;
using Loadout.Core.Storage;

namespace Loadout.App.Diagnostics;

/// <summary>Appends unhandled exceptions to error.log in the data directory, so crashes are diagnosable.</summary>
internal static class CrashLog
{
    public static string Path => System.IO.Path.Combine(LoadoutPaths.DataDirectory, "error.log");

    public static void Write(string source, Exception exception) => Write(source, exception.ToString());

    public static void Write(string source, string detail)
    {
        try
        {
            File.AppendAllText(Path, $"[{DateTimeOffset.Now:O}] {source}{Environment.NewLine}{detail}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException) { /* logging must never itself throw */ }
        catch (UnauthorizedAccessException) { /* ditto */ }
    }
}
