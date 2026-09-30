using System.IO;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.GuideGenerator;

public static class PrototypeListGenerator
{
    public static void PublishJson(Stream stream)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>();

        // Map: prototype kind name -> list of prototype ids of this kind.
        var output = new Dictionary<string, List<string>>();

        foreach (var kindType in proto.EnumeratePrototypeKinds())
        {
            var kindName = proto.TryGetKindFrom(kindType, out var actualKindName)
                ? actualKindName
                : kindType.Name;
            kindName = TextTools.CapitalizeString(kindName);
            var ids = new List<string>();

            foreach (var p in proto.EnumeratePrototypes(kindType))
            {
                ids.Add(p.ID);
            }

            if (ids.Count == 0)
                continue;

            output[kindName] = ids;
        }

        if (output.Count == 0)
            return;

        GuideJson.Write(stream, output);
    }
}
