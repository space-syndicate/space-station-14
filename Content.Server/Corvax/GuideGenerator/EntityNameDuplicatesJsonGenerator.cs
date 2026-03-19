using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Content.Shared.Labels.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.GuideGenerator;

public static class EntityNameDuplicatesJsonGenerator
{
    // Suffix parts that should be ignored when building display names.
    private static readonly HashSet<string> IgnoredSuffixTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "DO NOT MAP",
        "не маппить",
    };

    private static string GetLabel(EntityPrototype proto)
    {
        return proto.Components.Values
            .Select(x => x.Component)
            .OfType<LabelComponent>()
            .Select(lc => lc.CurrentLabel)
            .Where(label => !string.IsNullOrEmpty(label))
            .Select(label => Loc.GetString(label!))
            .FirstOrDefault() ?? string.Empty;
    }

    private static Dictionary<string, List<string>> GetDuplicatesName(
        IPrototypeManager prototypeManager,
        bool duplicatesOnly)
    {
        var loc = IoCManager.Resolve<ILocalizationManager>();
        return prototypeManager
            .EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract &&
                        p.Components.Values.Any(c => c.Component is FixturesComponent))
            .GroupBy(p =>
            {
                var name = TextTools.CapitalizeString(TextTools.GetDisplayName(p, prototypeManager, loc));

                var label = GetLabel(p);

                var rawSuffix = p.EditorSuffix;
                var suffix = string.Empty;
                if (!string.IsNullOrWhiteSpace(rawSuffix))
                {
                    var parts = rawSuffix
                        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => part.Trim())
                        .Where(part => !IgnoredSuffixTokens.Contains(part))
                        .ToArray();

                    if (parts.Length > 0)
                        suffix = string.Join(", ", parts).ToLowerInvariant();
                }

                return (Name: name, Label: label, Suffix: suffix);
            })
            .Where(g => !duplicatesOnly || g.Count() > 1)
            .ToDictionary(
                g =>
                {
                    var (name, label, suffix) = g.Key;

                    if (!string.IsNullOrEmpty(label) &&
                        !string.IsNullOrEmpty(suffix) &&
                        label.Equals(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        suffix = string.Empty;
                    }

                    if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(suffix))
                        return name;

                    if (string.IsNullOrEmpty(label))
                        return $"{name} ({suffix})";

                    if (string.IsNullOrEmpty(suffix))
                        return $"{name} ({label})";

                    return $"{name} ({label}) ({suffix})";
                },
                g => duplicatesOnly
                    ? g.Select(p => p.ID).OrderBy(id => id).ToList()
                    : [g.OrderBy(p => p.ID).First().ID]);
    }

    public static void PublishNameJson(StreamWriter writer)
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();

        var nameToIds = GetDuplicatesName(prototypeManager, false);
        var nameToSingleId = nameToIds.ToDictionary(kv => kv.Key, kv => kv.Value[0]);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        writer.Write(JsonSerializer.Serialize(nameToSingleId, options));
    }

    public static void PublishDuplicatesJson(StreamWriter writer)
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();

        var duplicatesName = GetDuplicatesName(prototypeManager, true);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        writer.Write(JsonSerializer.Serialize(duplicatesName, options));
    }
}
