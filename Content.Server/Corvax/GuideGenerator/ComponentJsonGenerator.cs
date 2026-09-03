using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.GuideGenerator;

public static class ComponentJsonGenerator
{
    public static void PublishAll(IResourceManager res, ResPath destination)
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var serializationManager = IoCManager.Resolve<ISerializationManager>();
        var componentFactory = IoCManager.Resolve<IComponentFactory>();
        var destinationRoot = new ResPath("component").ToRootedPath();

        // Map: component name -> (entity id -> component fields)
        var output = new Dictionary<string, Dictionary<string, object?>>();
        foreach (var entityPrototype in prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            foreach (var (componentName, componentFields) in BuildEntityComponentMap(
                         entityPrototype,
                         prototypeManager,
                         serializationManager,
                         componentFactory))
            {
                GetOrCreateEntry(output, componentName)[entityPrototype.ID] = componentFields;
            }
        }

        if (output.Count == 0)
            return;

        res.UserData.CreateDir(destinationRoot);
        foreach (var (componentName, fieldsByEntity) in output)
        {
            var defaultObject = FieldEntry.ComputeComponentDefault(
                componentName,
                componentFactory,
                serializationManager);
            var componentOutput = FieldEntry.DeduplicateAgainstDefault(defaultObject, fieldsByEntity);
            var directoryName = TextTools.CapitalizeString(componentName);
            var componentRoot = destinationRoot / directoryName;

            res.UserData.CreateDir(componentRoot);
            GuideJson.WriteFile(res, destinationRoot / $"{directoryName}.json", componentOutput);
            GuideJson.WriteFile(res, componentRoot / "defaultFields.json", defaultObject);
        }
    }

    private static Dictionary<string, object?> GetOrCreateEntry(Dictionary<string, Dictionary<string, object?>> output, string key)
    {
        if (!output.TryGetValue(key, out var map))
        {
            map = new Dictionary<string, object?>();
            output[key] = map;
        }

        return map;
    }

    public static Dictionary<string, object?> BuildEntityComponentMap(EntityPrototype entProto, IPrototypeManager proto, ISerializationManager ser, IComponentFactory compFactory)
    {
        var components = new Dictionary<string, object?>(StringComparer.Ordinal);
        var composedComponents = YAMLEntry.GetComposedComponentMappings(entProto, proto, ser, compFactory);

        foreach (var (compName, entry) in entProto.Components)
        {
            if (!FieldEntry.TryWriteValueAsMapping(ser, entry.Component.GetType(), entry.Component, out var node))
                continue;

            composedComponents.TryGetValue(compName, out var composedNode);
            components[compName] = FieldEntry.ProcessNode(entry.Component, node, composedNode);
        }

        foreach (var (compName, node) in composedComponents)
        {
            if (entProto.Components.ContainsKey(compName))
                continue;

            components[compName] = FieldEntry.ConvertNode(node);
        }

        return components;
    }
}
