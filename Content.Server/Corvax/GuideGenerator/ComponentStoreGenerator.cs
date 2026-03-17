using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.Corvax.GuideGenerator;

public static class ComponentStoreGenerator
{
    public static void PublishJson(StreamWriter file)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>();
        var compFactory = IoCManager.Resolve<IComponentFactory>();

        // Map: referenced entity id -> (component name -> list of entity prototype ids that reference it).
        var output = new Dictionary<string, Dictionary<string, HashSet<string>>>();

        foreach (var entityProto in proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.TryGetMapping(typeof(EntityPrototype), entityProto.ID, out var mapping))
                continue;

            if (!mapping.TryGet("components", out SequenceDataNode? componentsNode))
                continue;

            foreach (var componentNode in componentsNode)
            {
                if (componentNode is not MappingDataNode compMap)
                    continue;

                if (!compMap.TryGet("type", out ValueDataNode? typeNode))
                    continue;

                var compName = typeNode.Value;
                if (string.IsNullOrWhiteSpace(compName))
                    continue;

                Type compType;
                try
                {
                    var registration = compFactory.GetRegistration(compName);
                    compType = registration.Type;
                }
                catch
                {
                    continue;
                }

                var referencedEntityIds = new HashSet<string>();
                FieldStoreId.InspectTypeForEntityRefs(compType, compMap, referencedEntityIds);

                if (referencedEntityIds.Count == 0)
                    continue;

                foreach (var entId in referencedEntityIds)
                {
                    if (!output.TryGetValue(entId, out var byComponent))
                    {
                        byComponent = new Dictionary<string, HashSet<string>>();
                        output[entId] = byComponent;
                    }

                    if (!byComponent.TryGetValue(compName, out var entities))
                    {
                        entities = new HashSet<string>();
                        byComponent[compName] = entities;
                    }

                    entities.Add(entityProto.ID);
                }
            }
        }

        if (output.Count == 0)
            return;

        var normalized = new Dictionary<string, Dictionary<string, List<string>>>(output.Count);
        foreach (var (refEntId, byComponent) in output)
        {
            var compMap = new Dictionary<string, List<string>>(byComponent.Count);
            foreach (var (compName, entities) in byComponent)
            {
                var list = entities.ToList();
                list.Sort(StringComparer.Ordinal);
                compMap[compName] = list;
            }

            normalized[refEntId] = compMap;
        }

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        file.Write(JsonSerializer.Serialize(normalized, serializeOptions));
    }
}
