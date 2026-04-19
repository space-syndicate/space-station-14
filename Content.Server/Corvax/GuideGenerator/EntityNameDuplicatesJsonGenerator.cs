using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Content.Shared.Actions.Components;
using Content.Shared.Corvax.GuideGenerator;
using Content.Shared.Labels.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.GuideGenerator;

public static class EntityNameDuplicatesJsonGenerator
{
    private const string AbilitySuffix = "(способность)";

    public static readonly string[] AllowedNameComponents =
    [
        "Fixtures",
        "Physics",
        "Action"
    ];

    // Suffix parts that should be ignored when building display names.
    public static readonly HashSet<string> IgnoredSuffixTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "do not map",
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

    public static bool MatchesEntityNameFilter(EntityPrototype proto, IReadOnlySet<string> allowedIds)
    {
        var hasAllowedComponent = AllowedNameComponents.Any(proto.Components.ContainsKey);

        return !proto.Abstract &&
               hasAllowedComponent &&
               EntityProjectHelper.MatchesAllowedIds(proto.ID, allowedIds);
    }

    private static Dictionary<string, List<string>> GetDuplicatesName(
        IPrototypeManager prototypeManager,
        bool duplicatesOnly)
    {
        var loc = IoCManager.Resolve<ILocalizationManager>();
        var compFactory = IoCManager.Resolve<IComponentFactory>();
        var allowedIds = EntityProjectGenerator.GetProjectEntityIds();
        return prototypeManager
            .EnumeratePrototypes<EntityPrototype>()
            .Where(proto => MatchesEntityNameFilter(proto, allowedIds))
            .GroupBy(p =>
            {
                var name = TextTools.CapitalizeString(TextTools.GetDisplayName(p, prototypeManager, loc));

                if (p.TryGetComponent<ActionComponent>(out _, compFactory))
                    name = $"{name} {AbilitySuffix}";

                var label = GetLabel(p);

                var rawSuffix = p.EditorSuffix;
                var suffix = TextTools.GetEditorSuffix(rawSuffix, IgnoredSuffixTokens, TextTools.NormalizeSuffixToken);

                return (Name: name, Label: label, Suffix: suffix);
            })
            .Where(g => !string.IsNullOrWhiteSpace(g.Key.Name))
            .Where(g => !duplicatesOnly || g.Count() > 1)
            .ToDictionary(
                g =>
                {
                    var (name, label, suffix) = g.Key;

                    if (!string.IsNullOrEmpty(label) &&
                        !string.IsNullOrEmpty(suffix) &&
                        label.Trim().Equals(suffix.Trim(), StringComparison.OrdinalIgnoreCase))
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
