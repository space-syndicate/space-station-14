using System.IO;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.GuideGenerator;

public static class ComponentListGenerator
{
    public static void PublishJson(Stream stream)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>();

        // Map: component name -> list of entity ids.
        var output = new Dictionary<string, List<string>>();

        foreach (var p in proto.EnumeratePrototypes(typeof(EntityPrototype)))
        {
            if (p is not EntityPrototype entityProto)
                continue;

            foreach (var (compName, _) in entityProto.Components)
            {
                GetOrCreateEntry(output, compName).Add(entityProto.ID);
            }
        }

        if (output.Count == 0)
            return;

        foreach (var ids in output.Values)
        {
            ids.Sort();
        }

        GuideJson.Write(stream, output);
    }

    private static List<string> GetOrCreateEntry(Dictionary<string, List<string>> output, string key)
    {
        if (!output.TryGetValue(key, out var ids))
        {
            ids = new List<string>();
            output[key] = ids;
        }

        return ids;
    }
}
