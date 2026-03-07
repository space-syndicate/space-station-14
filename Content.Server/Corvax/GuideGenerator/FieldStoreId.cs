using System.Collections;
using System.Reflection;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.Corvax.GuideGenerator;

public static class FieldStoreId
{
    public static void InspectTypeForEntityRefs(Type prototypeType, MappingDataNode mapping, HashSet<string> outIds)
    {
        foreach (var field in prototypeType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = field.GetCustomAttribute<DataFieldAttribute>();
            if (attr == null)
                continue;

            var tag = attr.Tag ?? LowerFirst(field.Name);
            if (!mapping.TryGet(tag, out var node))
                continue;

            ExtractIdsFromNode(field.FieldType, node, outIds);
        }

        foreach (var prop in prototypeType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = prop.GetCustomAttribute<DataFieldAttribute>();
            if (attr == null)
                continue;

            var tag = attr.Tag ?? LowerFirst(prop.Name);
            if (!mapping.TryGet(tag, out var node))
                continue;

            ExtractIdsFromNode(prop.PropertyType, node, outIds);
        }
    }

    public static void ExtractIdsFromNode(Type memberType, DataNode node, HashSet<string> outIds)
    {
        var underlying = GetEffectiveUnderlyingType(memberType, node);

        if (IsEntProtoIdType(underlying))
        {
            if (node is ValueDataNode v)
            {
                if (!string.IsNullOrWhiteSpace(v.Value))
                    outIds.Add(v.Value);
            }
            else if (node is MappingDataNode m && m.TryGet("id", out var idNode) && idNode is ValueDataNode idVal)
            {
                if (!string.IsNullOrWhiteSpace(idVal.Value))
                    outIds.Add(idVal.Value);
            }
            return;
        }

        if (node is SequenceDataNode seq)
        {
            var elemType = GetElementType(underlying);
            if (elemType == null)
                return;

            foreach (var child in seq.Sequence)
            {
                ExtractIdsFromNode(elemType, child, outIds);
            }

            return;
        }

        if (node is MappingDataNode map)
        {
            if (typeof(IDictionary).IsAssignableFrom(underlying) && underlying.IsGenericType)
            {
                var args = underlying.GetGenericArguments();
                if (args.Length == 2)
                {
                    var valueType = args[1];
                    foreach (var (_, childNode) in map)
                    {
                        ExtractIdsFromNode(valueType, childNode, outIds);
                    }

                    return;
                }
            }

            foreach (var field in underlying.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var attr = field.GetCustomAttribute<DataFieldAttribute>();
                if (attr == null)
                    continue;

                var tag = attr.Tag ?? LowerFirst(field.Name);
                if (!map.TryGet(tag, out var childNode))
                    continue;

                ExtractIdsFromNode(field.FieldType, childNode, outIds);
            }

            foreach (var prop in underlying.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var attr = prop.GetCustomAttribute<DataFieldAttribute>();
                if (attr == null)
                    continue;

                var tag = attr.Tag ?? LowerFirst(prop.Name);
                if (!map.TryGet(tag, out var childNode))
                    continue;

                ExtractIdsFromNode(prop.PropertyType, childNode, outIds);
            }
        }
    }

    private static Type GetEffectiveUnderlyingType(Type memberType, DataNode node)
    {
        var underlying = Nullable.GetUnderlyingType(memberType) ?? memberType;

        if (node is not MappingDataNode map)
            return underlying;

        var tag = map.Tag;
        if (string.IsNullOrEmpty(tag))
            return underlying;

        const string prefix = "!type:";
        if (!tag.StartsWith(prefix, StringComparison.Ordinal))
            return underlying;

        var typeName = tag.Substring(prefix.Length);

        try
        {
            var ser = IoCManager.Resolve<ISerializationManager>();
            var reflection = ser.ReflectionManager;
            var actual = reflection.YamlTypeTagLookup(underlying, typeName);
            if (actual != null && underlying.IsAssignableFrom(actual))
                return actual;
        }
        catch
        {
            // ignored
        }

        return underlying;
    }

    private static bool IsEntProtoIdType(Type t)
    {
        if (t == typeof(EntProtoId))
            return true;

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(EntProtoId<>))
            return true;

        return false;
    }

    private static Type? GetElementType(Type t)
    {
        if (t.IsArray)
            return t.GetElementType();

        if (t.IsGenericType)
        {
            var genDef = t.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IEnumerable<>) || genDef == typeof(IReadOnlyList<>) || genDef == typeof(ICollection<>))
                return t.GetGenericArguments()[0];
        }

        return null;
    }

    private static string LowerFirst(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;
        if (s.Length == 1)
            return s.ToLowerInvariant();
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}
