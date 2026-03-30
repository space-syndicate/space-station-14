using System.Linq;
using System.Text.Json.Serialization;
using Content.Shared.Labels.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.Corvax.GuideGenerator;

public sealed class EntityEntry
{
    private static string[]? GetRootParents(EntityPrototype proto, IPrototypeManager prototypeManager)
    {
        if (proto.Parents is not { Length: > 0 })
            return null;

        var roots = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        static IEnumerable<string> GetParentIds(MappingDataNode mapping)
        {
            if (mapping.TryGet("parent", out ValueDataNode? parentValue))
            {
                if (!string.IsNullOrWhiteSpace(parentValue.Value))
                    yield return parentValue.Value;

                yield break;
            }

            if (!mapping.TryGet("parent", out SequenceDataNode? parentSequence))
                yield break;

            foreach (var parentNode in parentSequence)
            {
                if (parentNode is not ValueDataNode valueNode)
                    continue;

                if (!string.IsNullOrWhiteSpace(valueNode.Value))
                    yield return valueNode.Value;
            }
        }

        void Visit(string id)
        {
            if (!visited.Add(id))
                return;

            if (!YAMLEntry.TryGetRawMapping(prototypeManager, typeof(EntityPrototype), id, out var mapping) ||
                mapping == null)
            {
                roots.Add(id);
                return;
            }

            var parents = GetParentIds(mapping).ToArray();
            if (parents.Length == 0)
            {
                roots.Add(id);
                return;
            }

            foreach (var parent in parents)
            {
                Visit(parent);
            }
        }

        foreach (var parent in proto.Parents)
        {
            Visit(parent);
        }

        return roots.Count > 0 ? roots.OrderBy(x => x, StringComparer.Ordinal).ToArray() : null;
    }

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("desc")]
    public string Description { get; }

    [JsonPropertyName("suffix")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Suffix { get; }

    [JsonPropertyName("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; }

    [JsonPropertyName("parents")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Parents { get; }

    public EntityEntry(EntityPrototype proto)
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var loc = IoCManager.Resolve<ILocalizationManager>();

        Id = proto.ID;
        Name = TextTools.CapitalizeString(TextTools.GetDisplayName(proto, prototypeManager, loc));
        Description = proto.Description;
        Suffix = string.IsNullOrWhiteSpace(proto.EditorSuffix) ? null : proto.EditorSuffix;
        Parents = GetRootParents(proto, prototypeManager);

        Label = proto.Components.Values
            .Select(x => x.Component)
            .OfType<LabelComponent>()
            .Select(lc => lc.CurrentLabel)
            .Where(label => !string.IsNullOrEmpty(label))
            .Select(label => Loc.GetString(label!))
            .FirstOrDefault();
    }
}
