using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.Corvax.GuideGenerator;

public static class PrototypeListGenerator
{
    public static void PublishJson(StreamWriter file)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>();

        // Map: entity id -> (prototype kind name -> list of prototype ids that reference it).
        var output = new Dictionary<string, Dictionary<string, List<string>>>();

        foreach (var kindType in proto.EnumeratePrototypeKinds())
        {
            var kindName = PrototypeUtility.CalculatePrototypeName(kindType.Name);
            foreach (var p in proto.EnumeratePrototypes(kindType))
            {
                if (!proto.TryGetMapping(kindType, p.ID, out var mapping))
                    continue;

                var referencedEntityIds = new HashSet<string>();

                InspectTypeForEntityRefs(kindType, mapping, referencedEntityIds);

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

    private static void InspectTypeForEntityRefs(Type prototypeType, MappingDataNode mapping, HashSet<string> outIds)
    {
        // Check fields.
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

        // Check properties.
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

    private static void ExtractIdsFromNode(Type memberType, DataNode node, HashSet<string> outIds)
    {
        var underlying = Nullable.GetUnderlyingType(memberType) ?? memberType;

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
