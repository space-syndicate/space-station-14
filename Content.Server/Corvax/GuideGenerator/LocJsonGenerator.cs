using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        var keys = new Dictionary<string, HashSet<string>>();

        // Matches top-level message/term identifiers at start of line (no leading whitespace or comment).
        var topEntryRegex = new Regex(@"(?m)^(?!\s|#)([^\s=]+)\s*=", RegexOptions.Compiled);
        // Matches attribute lines like "    .attr-name ="
        var attrRegex = new Regex(@"(?m)^\s*\.(?<name>[A-Za-z0-9_\-]+)\s*=", RegexOptions.Compiled);

        foreach (var path in files)
        {
            using var stream = res.ContentFileRead(path);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var contents = reader.ReadToEnd();
            // Normalize line endings to simplify indexing.
            contents = contents.Replace("\r\n", "\n");

            var matches = topEntryRegex.Matches(contents);
            for (var mi = 0; mi < matches.Count; mi++)
            {
                var m = matches[mi];
                var id = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(id))
                    continue;

                if (!keys.ContainsKey(id))
                    keys[id] = new HashSet<string>();

                var start = m.Index;
                var end = mi + 1 < matches.Count ? matches[mi + 1].Index : contents.Length;
                var block = contents.Substring(start, end - start);

                var attrMatches = attrRegex.Matches(block);
                foreach (Match am in attrMatches)
                {
                    var attrName = am.Groups["name"].Value;
                    if (!string.IsNullOrEmpty(attrName))
                        keys[id].Add(attrName);
                }
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
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        file.Write(JsonSerializer.Serialize(output, serializeOptions));
    }
}
