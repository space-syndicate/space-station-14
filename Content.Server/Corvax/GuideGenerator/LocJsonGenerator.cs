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
        var keysValues = new Dictionary<string, string>();

        // Matches top-level message/term identifiers at start of line (no leading whitespace or comment).
        var topEntryRegex = new Regex(@"^(?!\s|#)([^\s=]+)\s*=", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        // Matches attribute lines like "    .attr-name ="
        var attrRegex = new Regex(@"^\s*\.(?<name>[A-Za-z0-9_\-]+)\s*=", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);

        foreach (var path in files)
        {
            using var stream = res.ContentFileRead(path);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var contents = reader.ReadToEnd().Replace("\r\n", "\n");

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

                var mainEqual = block.IndexOf('=');
                if (mainEqual >= 0)
                {
                    var mainValueStart = mainEqual + 1;
                    var mainValueEnd = attrMatches.Count > 0 ? attrMatches[0].Index : block.Length;
                    keysValues[id] = mainValueEnd > mainValueStart ? FluentValue(block.Substring(mainValueStart, mainValueEnd - mainValueStart)) : string.Empty;
                }
                else
                {
                    keysValues[id] = string.Empty;
                }

                for (var ai = 0; ai < attrMatches.Count; ai++)
                {
                    var am = attrMatches[ai];
                    var attrName = am.Groups["name"].Value;
                    if (string.IsNullOrEmpty(attrName))
                        continue;
                    keys[id].Add(attrName);

                    var attrEqual = block.IndexOf('=', am.Index);
                    var attrValueStart = attrEqual >= 0 ? attrEqual + 1 : am.Index + am.Length;
                    var nextAttrIndex = ai + 1 < attrMatches.Count ? attrMatches[ai + 1].Index : block.Length;
                    keysValues[$"{id}.{attrName}"] = nextAttrIndex > attrValueStart ? FluentValue(block.Substring(attrValueStart, nextAttrIndex - attrValueStart)) : string.Empty;
                }

                continue;

                // Helper: remove leading newline and common indentation across non-empty lines.
                string FluentValue(string val)
                {
                    if (string.IsNullOrEmpty(val))
                        return string.Empty;

                    if (val.Length > 0 && val[0] == '\n')
                        val = val.Substring(1);

                    var lines = val.Split('\n');
                    lines = lines.Where(l => !(l.Length > 0 && l[0] == '#')).ToArray();
                    var nonEmptyLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
                    if (nonEmptyLines.Length == 0)
                        return string.Join("\n", lines).TrimEnd('\n');

                    var minIndent = nonEmptyLines.Min(l => l.TakeWhile(char.IsWhiteSpace).Count());
                    if (minIndent > 0)
                    {
                        for (var li = 0; li < lines.Length; li++)
                        {
                            var line = lines[li];
                            if (line.Length >= minIndent)
                                lines[li] = line.Substring(minIndent);
                        }
                    }

                    var result = string.Join("\n", lines);
                    if (result.EndsWith("\n"))
                        result = result.Substring(0, result.Length - 1);
                    return result;
                }
            }
        }

        // Build JSON dictionary using Loc.GetString for main key and for attributes.
        var output = new Dictionary<string, object?>();
        foreach (var (id, attrs) in keys.OrderBy(k => k.Key))
        {
            if (attrs.Count == 0)
            {
                output[id] = keysValues.TryGetValue(id, out var value) ? value : string.Empty;
            }
            else
            {
                // _value is the main value of the key.
                var obj = new Dictionary<string, string?>
                {
                    ["_value"] = keysValues.TryGetValue(id, out var valueMain) ? valueMain : string.Empty,
                };
                foreach (var attr in attrs.OrderBy(a => a))
                {
                    obj[attr] = keysValues.TryGetValue($"{id}.{attr}", out var valueAttr) ? valueAttr : string.Empty;
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
