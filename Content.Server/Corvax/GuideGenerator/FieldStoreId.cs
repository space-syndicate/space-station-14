using System.Collections;
using System.Linq;
using System.Reflection;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

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

            ExtractIdsFromNode(field.FieldType, node, outIds, attr.CustomTypeSerializer);
        }

        foreach (var prop in prototypeType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = prop.GetCustomAttribute<DataFieldAttribute>();
            if (attr == null)
                continue;

            var tag = attr.Tag ?? LowerFirst(prop.Name);
            if (!mapping.TryGet(tag, out var node))
                continue;

            ExtractIdsFromNode(prop.PropertyType, node, outIds, attr.CustomTypeSerializer);
        }
    }

    public static void ExtractIdsFromNode(Type memberType, DataNode node, HashSet<string> outIds, Type? customTypeSerializer = null)
    {
        var underlying = ResolveConcreteType(Nullable.GetUnderlyingType(memberType) ?? memberType, node);

        if (TryGetEntityPrototypeSerializerKind(customTypeSerializer, out var serializerKind))
        {
            ExtractIdsFromCustomSerializer(serializerKind, node, outIds, underlying);
            return;
        }

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

    private enum EntityPrototypeSerializerKind
    {
        Single,
        Sequence,
        DictionaryKey,
        DictionaryValue
    }

    private static bool TryGetEntityPrototypeSerializerKind(Type? serializerType, out EntityPrototypeSerializerKind kind)
    {
        kind = default;
        if (serializerType == null || !serializerType.IsGenericType)
            return false;

        var def = serializerType.GetGenericTypeDefinition();
        var args = serializerType.GetGenericArguments();

        if ((def == typeof(PrototypeIdSerializer<>) || def == typeof(AbstractPrototypeIdSerializer<>)) &&
            args[0] == typeof(EntityPrototype))
        {
            kind = EntityPrototypeSerializerKind.Single;
            return true;
        }

        if ((def == typeof(PrototypeIdListSerializer<>) || def == typeof(AbstractPrototypeIdListSerializer<>)
             || def == typeof(PrototypeIdHashSetSerializer<>) || def == typeof(AbstractPrototypeIdHashSetSerializer<>)
             || def == typeof(PrototypeIdArraySerializer<>) || def == typeof(AbstractPrototypeIdArraySerializer<>)) &&
            args[0] == typeof(EntityPrototype))
        {
            kind = EntityPrototypeSerializerKind.Sequence;
            return true;
        }

        if ((def == typeof(PrototypeIdDictionarySerializer<,>) || def == typeof(AbstractPrototypeIdDictionarySerializer<,>)) &&
            args[1] == typeof(EntityPrototype))
        {
            kind = EntityPrototypeSerializerKind.DictionaryKey;
            return true;
        }

        if ((def == typeof(PrototypeIdValueDictionarySerializer<,>) || def == typeof(AbstractPrototypeIdValueDictionarySerializer<,>)) &&
            args[1] == typeof(EntityPrototype))
        {
            kind = EntityPrototypeSerializerKind.DictionaryValue;
            return true;
        }

        return false;
    }

    private static void ExtractIdsFromCustomSerializer(EntityPrototypeSerializerKind kind, DataNode node, HashSet<string> outIds, Type declaredType)
    {
        switch (kind)
        {
            case EntityPrototypeSerializerKind.Single:
                ExtractIdFromNode(node, outIds);
                return;
            case EntityPrototypeSerializerKind.Sequence:
                if (node is SequenceDataNode seq)
                {
                    foreach (var child in seq.Sequence)
                    {
                        ExtractIdFromNode(child, outIds);
                    }
                }
                return;
            case EntityPrototypeSerializerKind.DictionaryKey:
            {
                if (node is MappingDataNode mapKeys)
                {
                    foreach (var key in mapKeys.Keys)
                    {
                        if (!string.IsNullOrWhiteSpace(key))
                            outIds.Add(key);
                    }
                }

                if (TryGetDictionaryValueType(declaredType, out var valueType) && node is MappingDataNode mapVals)
                {
                    foreach (var (_, childNode) in mapVals)
                    {
                        ExtractIdsFromNode(valueType, childNode, outIds);
                    }
                }
                return;
            }
            case EntityPrototypeSerializerKind.DictionaryValue:
            {
                if (node is MappingDataNode mapVals)
                {
                    foreach (var (_, child) in mapVals)
                    {
                        ExtractIdFromNode(child, outIds);
                    }
                }
                return;
            }
            default:
                return;
        }
    }

    private static void ExtractIdFromNode(DataNode node, HashSet<string> outIds)
    {
        if (node is ValueDataNode v)
        {
            if (!string.IsNullOrWhiteSpace(v.Value))
                outIds.Add(v.Value);
            return;
        }

        if (node is MappingDataNode m && m.TryGet("id", out var idNode) && idNode is ValueDataNode idVal)
        {
            if (!string.IsNullOrWhiteSpace(idVal.Value))
                outIds.Add(idVal.Value);
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

    private static bool TryGetDictionaryValueType(Type t, out Type valueType)
    {
        valueType = null!;
        if (!typeof(IDictionary).IsAssignableFrom(t) || !t.IsGenericType)
            return false;

        var args = t.GetGenericArguments();
        if (args.Length != 2)
            return false;

        valueType = args[1];
        return true;
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
