using System.Collections;
using System.Linq;
using System.Reflection;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.Corvax.GuideGenerator;

public static class FieldStoreId
{
    private const string TypeTagPrefix = "!type:";
    private static readonly Dictionary<(Type DeclaredType, string Tag), Type> ConcreteTypeCache = new();
    private static readonly Dictionary<Type, Type[]> ConcreteAssignableTypesCache = new();
    private static readonly Dictionary<Type, HashSet<string>> DataFieldTagsCache = new();

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
        var underlying = ResolveConcreteType(Nullable.GetUnderlyingType(memberType) ?? memberType, node);

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

    private static bool IsEntProtoIdType(Type t)
    {
        if (t == typeof(EntProtoId))
            return true;

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(EntProtoId<>))
            return true;

        return false;
    }

    private static Type ResolveConcreteType(Type declaredType, DataNode node)
    {
        if (node is MappingDataNode map)
        {
            var inferred = InferConcreteType(declaredType, map);
            if (inferred != declaredType)
                return inferred;
        }

        if (node.Tag == null || !node.Tag.StartsWith(TypeTagPrefix, StringComparison.Ordinal))
            return declaredType;

        var typeName = node.Tag[TypeTagPrefix.Length..];
        if (string.IsNullOrWhiteSpace(typeName))
            return declaredType;

        var cacheKey = (declaredType, typeName);
        if (ConcreteTypeCache.TryGetValue(cacheKey, out var cachedType))
            return cachedType;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).Cast<Type>().ToArray();
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (!declaredType.IsAssignableFrom(type))
                    continue;

                if (!string.Equals(type.Name, typeName, StringComparison.Ordinal))
                    continue;

                ConcreteTypeCache[cacheKey] = type;
                return type;
            }
        }

        ConcreteTypeCache[cacheKey] = declaredType;
        return declaredType;
    }

    private static Type InferConcreteType(Type declaredType, MappingDataNode node)
    {
        var nodeKeys = node.Keys.ToHashSet(StringComparer.Ordinal);
        if (nodeKeys.Count == 0)
            return declaredType;

        var candidates = GetConcreteAssignableTypes(declaredType);
        if (candidates.Length == 0)
            return declaredType;

        Type? bestType = null;
        var bestScore = -1;
        var ambiguous = false;

        foreach (var candidate in candidates)
        {
            var candidateTags = GetDataFieldTags(candidate);
            if (candidateTags.Count == 0)
                continue;

            if (!nodeKeys.IsSubsetOf(candidateTags))
                continue;

            var score = nodeKeys.Count;

            if (score > bestScore)
            {
                bestType = candidate;
                bestScore = score;
                ambiguous = false;
                continue;
            }

            if (score == bestScore)
                ambiguous = true;
        }

        if (ambiguous || bestType == null)
            return declaredType;

        return bestType;
    }

    private static Type[] GetConcreteAssignableTypes(Type declaredType)
    {
        if (ConcreteAssignableTypesCache.TryGetValue(declaredType, out var cached))
            return cached;

        var types = new List<Type>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] assemblyTypes;
            try
            {
                assemblyTypes = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                assemblyTypes = e.Types.Where(t => t != null).Cast<Type>().ToArray();
            }

            foreach (var type in assemblyTypes)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (!declaredType.IsAssignableFrom(type))
                    continue;

                types.Add(type);
            }
        }

        cached = types.ToArray();
        ConcreteAssignableTypesCache[declaredType] = cached;
        return cached;
    }

    private static HashSet<string> GetDataFieldTags(Type type)
    {
        if (DataFieldTagsCache.TryGetValue(type, out var cached))
            return cached;

        var tags = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = field.GetCustomAttribute<DataFieldAttribute>();
            if (attr == null)
                continue;

            tags.Add(attr.Tag ?? LowerFirst(field.Name));
        }

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = prop.GetCustomAttribute<DataFieldAttribute>();
            if (attr == null)
                continue;

            tags.Add(attr.Tag ?? LowerFirst(prop.Name));
        }

        DataFieldTagsCache[type] = tags;
        return tags;
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
