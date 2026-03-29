using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.GuideGenerator;

public static class PrototypeStoreGenerator
{
    public static void PublishJson(StreamWriter file)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>();

        // Map: entity id -> (prototype kind name -> list of prototype ids that reference it).
        var output = new Dictionary<string, Dictionary<string, List<string>>>();

        foreach (var kindType in proto.EnumeratePrototypeKinds())
        {
            var kindName = proto.TryGetKindFrom(kindType, out var actualKindName)
                ? actualKindName
                : kindType.Name;
            kindName = TextTools.CapitalizeString(kindName);
            foreach (var p in proto.EnumeratePrototypes(kindType))
            {
                if (!proto.TryGetMapping(kindType, p.ID, out var mapping))
                    continue;

                var referencedEntityIds = new HashSet<string>();

                FieldStoreId.InspectTypeForEntityRefs(kindType, mapping, referencedEntityIds);

                if (referencedEntityIds.Count == 0)
                    continue;

                foreach (var entId in referencedEntityIds)
                {
                    if (!output.TryGetValue(entId, out var byKind))
                    {
                        byKind = new Dictionary<string, List<string>>();
                        output[entId] = byKind;
                    }

                    if (!byKind.TryGetValue(kindName, out var list))
                    {
                        list = new List<string>();
                        byKind[kindName] = list;
                    }

                    list.Add(p.ID);
                }
            }
        }

        if (output.Count == 0)
            return;

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        file.Write(JsonSerializer.Serialize(output, serializeOptions));
    }

}
