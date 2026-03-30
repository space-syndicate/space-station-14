using System.Reflection;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.Corvax.GuideGenerator;

public static class YAMLEntry
{
    private static readonly FieldInfo? KindsField = FindField(typeof(PrototypeManager), "_kinds");
    private static readonly MethodInfo? TryGetValueMethod = typeof(Dictionary<string, MappingDataNode>)
        .GetMethod(nameof(Dictionary<string, MappingDataNode>.TryGetValue));

    public static FieldInfo? FindField(Type type, string name)
    {
        while (true)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
                return field;

            if (type.BaseType == null)
                return null;

            type = type.BaseType;
        }
    }

    public static bool TryGetRawMapping(
        IPrototypeManager proto,
        Type kind,
        string id,
        out MappingDataNode? mapping)
    {
        mapping = null;

        if (KindsField?.GetValue(proto) is not object kinds)
            return false;

        var itemProperty = kinds.GetType().GetProperty("Item");
        var kindData = itemProperty?.GetValue(kinds, new object[] { kind });
        if (kindData == null)
            return false;

        var rawResultsField = FindField(kindData.GetType(), "RawResults");
        var rawResults = rawResultsField?.GetValue(kindData);
        if (rawResults == null || TryGetValueMethod == null)
            return false;

        var args = new object?[] { id, null };
        var found = (bool) TryGetValueMethod.Invoke(rawResults, args)!;
        mapping = (MappingDataNode?) args[1];
        return found;
    }

    public static Dictionary<string, MappingDataNode> GetComposedComponentMappings(
        EntityPrototype entProto,
        IPrototypeManager proto,
        ISerializationManager ser,
        IComponentFactory compFactory)
    {
        var composed = new Dictionary<string, MappingDataNode>(StringComparer.Ordinal);
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

            if (!TryGetRawMapping(proto, typeof(EntityPrototype), id, out var mapping) || mapping == null)
                return;

            foreach (var parent in GetParentIds(mapping))
            {
                Visit(parent);
            }

            if (!mapping.TryGet("components", out SequenceDataNode? componentsNode))
                return;

            foreach (var componentNode in componentsNode)
            {
                if (componentNode is not MappingDataNode compMap)
                    continue;

                if (!compMap.TryGet("type", out ValueDataNode? typeNode))
                    continue;

                var compName = typeNode.Value;
                if (string.IsNullOrWhiteSpace(compName))
                    continue;

                var child = compMap.Copy();
                child.Remove("type");

                if (!composed.TryGetValue(compName, out var parent))
                {
                    composed[compName] = child;
                    continue;
                }

                if (compFactory.TryGetRegistration(compName, out var registration))
                {
                    composed[compName] = ser.PushCompositionWithGenericNode(registration.Type, parent, child);
                    continue;
                }

                composed[compName] = MergeMappings(parent, child);
            }
        }

        Visit(entProto.ID);
        return composed;
    }

    public static MappingDataNode MergeMappings(MappingDataNode parent, MappingDataNode child)
    {
        var merged = parent.Copy();

        foreach (var (key, value) in child)
        {
            if (merged.TryGetValue(key, out var existing) &&
                existing is MappingDataNode existingMap &&
                value is MappingDataNode childMap)
            {
                merged[key] = MergeMappings(existingMap, childMap);
                continue;
            }

            merged[key] = value.Copy();
        }

        return merged;
    }
}
