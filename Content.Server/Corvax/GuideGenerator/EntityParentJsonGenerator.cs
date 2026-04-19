using System.Linq;
using System.Text.Json.Serialization;
using System.IO;
using System.Text.Json;
using System.Text.Encodings.Web;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.Corvax.GuideGenerator;

public sealed class EntityParentJsonGenerator
{
    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("parents")]
    public string[] Parents { get; }

    public EntityParentJsonGenerator(EntityPrototype proto)
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();

        Id = proto.ID;
        Parents = GetParents(proto, prototypeManager);
    }

    public static string[] GetParents(EntityPrototype proto, IPrototypeManager prototypeManager)
    {
        var parents = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !visited.Add(id))
                return;

            parents.Add(id);

            if (!YAMLEntry.TryGetRawMapping(prototypeManager, typeof(EntityPrototype), id, out var mapping) ||
                mapping == null)
            {
                return;
            }

            foreach (var parent in GetParentIds(mapping))
            {
                Visit(parent);
            }
        }

        foreach (var parent in proto.Parents ?? [])
        {
            Visit(parent);
        }

        return parents.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> GetParentIds(MappingDataNode mapping)
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

    public static void PublishJson(StreamWriter file)
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var prototypes = prototypeManager
            .EnumeratePrototypes<EntityPrototype>()
            .Where(x => !x.Abstract)
            .Select(x => new EntityParentJsonGenerator(x))
            .ToDictionary(x => x.Id, x => x.Parents);

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        file.Write(JsonSerializer.Serialize(prototypes, serializeOptions));
    }
}
