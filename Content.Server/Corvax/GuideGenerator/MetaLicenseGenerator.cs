using System.IO;
using System.Text.Json;

namespace Content.Server.Corvax.GuideGenerator;

public static class MetaLicenseGenerator
{
    public static void PublishJson(Stream stream)
    {
        var workingDir = Directory.GetCurrentDirectory();
        var resourcesRoot = Path.Combine(workingDir, "Resources");
        if (!Directory.Exists(resourcesRoot))
            return;

        var output = new Dictionary<string, Dictionary<string, string>>();

        foreach (var metaPath in Directory.EnumerateFiles(resourcesRoot, "meta.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(metaPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var license = root.TryGetProperty("license", out var licEl) && licEl.ValueKind == JsonValueKind.String
                    ? licEl.GetString() ?? string.Empty
                    : string.Empty;

                var copyright = root.TryGetProperty("copyright", out var copyEl) && copyEl.ValueKind == JsonValueKind.String
                    ? copyEl.GetString() ?? string.Empty
                    : string.Empty;
                var resourceDir = Path.GetDirectoryName(metaPath) ?? metaPath;
                var relativeResourcePath = Path.GetRelativePath(workingDir, resourceDir).Replace('\\', '/');

                output[relativeResourcePath] = new Dictionary<string, string>
                    {
                        { "license", license },
                        { "copyright", copyright }
                    };
            }
            catch
            {
                // Ignore
            }
        }

        if (output.Count == 0)
            return;

        GuideJson.Write(stream, output);
    }
}
