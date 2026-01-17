using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.GuideGenerator;

public static class ComponentListGenerator
{
    public static void PublishJson(StreamWriter file)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>();

        // Map: entity id -> list of component names.
        var output = new Dictionary<string, List<string>>();

        foreach (var p in proto.EnumeratePrototypes(typeof(EntityPrototype)))
        {
            if (p is not EntityPrototype entityProto)
                continue;

            var componentNames = new List<string>();
            foreach (var (compName, _) in entityProto.Components)
            {
                componentNames.Add(compName);
            }

            if (componentNames.Count > 0)
                output[entityProto.ID] = componentNames;
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
