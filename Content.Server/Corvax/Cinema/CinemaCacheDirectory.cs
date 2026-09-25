using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Content.Server.Corvax.Cinema;

/// <summary>Identifies cache owners across crashes without deleting another live server's files.</summary>
internal static class CinemaCacheDirectory
{
    private const string Prefix = "covax-cinema-audio-";

    internal static string NewPath(string temporaryRoot)
    {
        using var process = Process.GetCurrentProcess();
        return Path.Combine(temporaryRoot,
            $"{Prefix}{process.Id}-{process.StartTime.ToUniversalTime().Ticks}-{Guid.NewGuid():N}");
    }

    internal static void RemoveStale(string temporaryRoot, Action<string> warning)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(temporaryRoot, Prefix + "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                        continue;
                    var parts = Path.GetFileName(directory)[Prefix.Length..].Split('-');
                    // Old, ownerless caches cannot be distinguished from a running older server's cache.
                    // Leave those and unrelated directories alone; all newly created caches carry ownership.
                    if (parts.Length != 3 ||
                        !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid <= 0 ||
                        !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var start) || start <= 0 ||
                        !Guid.TryParseExact(parts[2], "N", out _) || IsOwnerAlive(pid, start))
                        continue;
                    Directory.Delete(directory, recursive: true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    warning($"Unable to remove stale cinema cache '{directory}': {e.Message}");
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warning($"Unable to scan stale cinema caches: {e.Message}");
        }
    }

    private static bool IsOwnerAlive(int pid, long start)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.StartTime.ToUniversalTime().Ticks == start;
        }
        catch (ArgumentException)
        {
            return false; // The owning process no longer exists.
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // If ownership cannot be checked, preserving a possibly active cache is safer than deleting it.
            return true;
        }
    }
}
