using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.GuideGenerator;

public static class LocJsonGenerator
{
    public static void PublishJson(StreamWriter file)
    {
        var loc = IoCManager.Resolve<ILocalizationManager>();
        var res = IoCManager.Resolve<IResourceManager>();

        // Choose culture: default if set, otherwise first found culture.
        var culture = loc.DefaultCulture ?? loc.GetFoundCultures().FirstOrDefault();
        if (culture == null)
        {
            file.Write("{}");
            return;
        }

        var root = new ResPath($"/Locale/{culture.Name}");
        var files = res.ContentFindFiles(root)
            .Where(c => c.Filename.EndsWith(".ftl", StringComparison.InvariantCultureIgnoreCase))
            .ToArray();

        // Parse files naively to collect top-level message ids and their attribute names.
        var keys = new Dictionary<string, HashSet<string>>();
        foreach (var path in files)
        {
            using var stream = res.ContentFileRead(path);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var contents = reader.ReadToEnd();

            // Simple line-oriented parser: detect top-level message lines and ".attr" lines indented under them.
            var lines = contents.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Top-level message: starts with non-whitespace and contains '='
                if (char.IsWhiteSpace(line, 0))
                    continue;

                var trimmedLine = line.TrimStart();

                if (trimmedLine.StartsWith("#"))
                    continue;

                var eq = line.IndexOf('=');
                if (eq == -1)
                    continue;

                var id = line.Substring(0, eq).Trim();
                if (string.IsNullOrEmpty(id))
                    continue;

                if (!keys.ContainsKey(id))
                    keys[id] = new HashSet<string>();

                // Collect following indented attribute lines.
                var j = i + 1;
                for (; j < lines.Length; j++)
                {
                    var next = lines[j];
                    if (string.IsNullOrWhiteSpace(next))
                        continue;

                    if (!char.IsWhiteSpace(next, 0))
                        break;

                    var trimmed = next.TrimStart();
                    if (!trimmed.StartsWith("."))
                        continue;

                    var attrEq = trimmed.IndexOf('=');
                    if (attrEq == -1)
                        continue;

                    var attrName = trimmed.Substring(1, attrEq - 1).Trim();
                    if (!string.IsNullOrEmpty(attrName))
                        keys[id].Add(attrName);
                }

                i = j - 1;
            }
        }

        // Build JSON dictionary using Loc.GetString for main key and for attributes.
        var output = new Dictionary<string, object?>();
        foreach (var (id, attrs) in keys.OrderBy(k => k.Key))
        {
            if (attrs.Count == 0)
            {
                output[id] = Loc.GetString(id);
            }
            else
            {
                // _value is the main value of the key.
                var obj = new Dictionary<string, string?>
                {
                    ["_value"] = Loc.GetString(id),
                };
                foreach (var attr in attrs.OrderBy(a => a))
                {
                    obj[attr] = Loc.GetString($"{id}.{attr}");
                }
                output[id] = obj;
            }
        }

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        file.Write(JsonSerializer.Serialize(output, serializeOptions));
    }
}
