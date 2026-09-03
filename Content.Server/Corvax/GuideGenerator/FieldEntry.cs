using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.Corvax.GuideGenerator;

public static class FieldEntry
{
    private const string IdField = "id";
    private const string TypeField = "type";
    private const string TypeKeyPrefix = "type:";

    private static readonly Regex DecimalValueRegex = new(
        @"^[+-]?\d+\.\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static object? ProcessNode(object instance, MappingDataNode node, MappingDataNode? composed = null)
    {
        NormalizeFlagsToSequences(instance, node);
        SupplementReadOnlyFields(instance.GetType(), node, composed);
        return DataNodeToObject(node);
    }

    public static object? ComputePrototypeDefault(Type prototypeType, ISerializationManager serializationManager)
    {
        var instance = TryCreateInstance(prototypeType);
        if (instance == null)
            return null;

        return SerializeDefault(instance, prototypeType, serializationManager, removeId: true);
    }

    public static object? ComputeComponentDefault(
        string componentName,
        IComponentFactory componentFactory,
        ISerializationManager serializationManager)
    {
        if (!componentFactory.TryGetRegistration(componentName, out var registration))
            return null;

        try
        {
            var component = componentFactory.GetComponent(registration.Type);
            return SerializeDefault(component, component.GetType(), serializationManager, removeId: false);
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    public static bool TryWriteValueAsMapping(
        ISerializationManager serializationManager,
        Type type,
        object value,
        out MappingDataNode node,
        bool alwaysWrite = false)
    {
        try
        {
            node = serializationManager.WriteValueAs<MappingDataNode>(type, value, alwaysWrite);
            return true;
        }
        catch
        {
            node = new MappingDataNode();
            return false;
        }
    }

    public static Dictionary<string, object?> DeduplicateAgainstDefault(
        object? defaultObject,
        Dictionary<string, object?> values)
    {
        var defaults = defaultObject as Dictionary<string, object?> ?? new();
        foreach (var value in values.Values)
        {
            if (value is Dictionary<string, object?> fields)
                RemoveDefaultDuplicates(defaults, fields);
        }

        return new Dictionary<string, object?>
        {
            ["default"] = defaultObject,
            [IdField] = values
        };
    }

    public static object? DataNodeToObject(DataNode node)
    {
        if (node is MappingDataNode mapping)
            return ConvertMapping(mapping);

        if (node is SequenceDataNode sequence)
            return ConvertSequence(sequence);

        if (node is ValueDataNode value)
            return ConvertValue(value);

        return node.ToString();
    }

    public static void SupplementReadOnlyFields(
        Type type,
        MappingDataNode serialized,
        MappingDataNode? composed)
    {
        if (composed == null)
            return;

        foreach (var member in GetSerializedMembers(type))
        {
            if (!member.Attribute!.ReadOnly || serialized.Has(member.Tag) || !composed.Has(member.Tag))
                continue;

            serialized[member.Tag] = composed[member.Tag].Copy();
        }
    }

    public static void NormalizeFlagsToSequences(object instance, MappingDataNode node)
    {
        var members = GetSerializedMembers(instance.GetType()).ToArray();
        foreach (var key in node.Keys.ToList())
        {
            var member = members.FirstOrDefault(candidate =>
                string.Equals(candidate.Tag, key, StringComparison.OrdinalIgnoreCase));

            if (member == null || !IsFlagsEnum(member.Type))
                continue;

            var value = member.GetValue(instance);
            if (value == null)
                continue;

            var numericValue = Convert.ToInt64(value);
            var names = Enum.GetValues(member.Type)
                .Cast<object>()
                .Select(flag => (Name: Enum.GetName(member.Type, flag)!, Value: Convert.ToInt64(flag)))
                .Where(flag => IsSingleSetFlag(flag.Value, numericValue))
                .Select(flag => flag.Name)
                .ToArray();

            node[key] = new SequenceDataNode(names);
        }
    }

    public static void EnsureFieldsCollectionsInitialized(object instance)
    {
        // A serializer only emits some nullable fields when their object graph exists.
        // Build that graph only when every nullable member has a safe default shape.
        if (CanInitializeMembers(instance, new HashSet<Type>()))
            InitializeMembers(instance, new HashSet<Type>());
    }

    public static Type GetMemberType(MemberInfo member)
    {
        if (member is PropertyInfo property)
            return property.PropertyType;

        if (member is FieldInfo field)
            return field.FieldType;

        throw new ArgumentException($"Unsupported member type: {member.GetType()}", nameof(member));
    }

    private static object? SerializeDefault(
        object instance,
        Type type,
        ISerializationManager serializationManager,
        bool removeId)
    {
        try
        {
            EnsureFieldsCollectionsInitialized(instance);
            if (!TryWriteValueAsMapping(serializationManager, type, instance, out var node, alwaysWrite: true))
                return new Dictionary<string, object?>();

            if (removeId)
                node.Remove(IdField);

            NormalizeFlagsToSequences(instance, node);
            return DataNodeToObject(node);
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
        finally
        {
            (instance as IDisposable)?.Dispose();
        }
    }

    private static object? ConvertMapping(MappingDataNode mapping)
    {
        var result = mapping.ToDictionary(pair => pair.Key, pair => DataNodeToObject(pair.Value));
        return mapping.Tag == null
            ? result
            : new Dictionary<string, object?> { [mapping.Tag] = result };
    }

    private static object ConvertSequence(SequenceDataNode sequence)
    {
        var items = sequence.Select(DataNodeToObject).ToList();
        var typedMap = new Dictionary<string, object?>();

        foreach (var item in items)
        {
            if (item is not Dictionary<string, object?> dictionary ||
                !dictionary.TryGetValue(TypeField, out var type) ||
                type == null)
            {
                return items;
            }

            var fields = new Dictionary<string, object?>(dictionary);
            fields.Remove(TypeField);
            typedMap[$"{TypeKeyPrefix}{type}"] = fields;
        }

        return typedMap.Count == 0 ? items : typedMap;
    }

    private static object? ConvertValue(ValueDataNode value)
    {
        if (value.IsNull)
            return null;

        var raw = value.Value;
        object parsed = ParseValue(raw);
        if (value.Tag == null)
            return parsed;

        return new Dictionary<string, object?>
        {
            [value.Tag] = string.IsNullOrEmpty(raw) ? new Dictionary<string, object?>() : parsed
        };
    }

    private static object ParseValue(string raw)
    {
        if (bool.TryParse(raw, out var boolean))
            return boolean;

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            return integer;

        if (DecimalValueRegex.IsMatch(raw) &&
            double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return raw;
    }

    private static bool AreEqual(object? first, object? second)
    {
        if (first == null || second == null)
            return first == null && second == null;

        if (first is IDictionary<string, object?> firstMap &&
            second is IDictionary<string, object?> secondMap)
        {
            return firstMap.Count == secondMap.Count &&
                   firstMap.All(pair => secondMap.TryGetValue(pair.Key, out var value) && AreEqual(pair.Value, value));
        }

        if (first is IList firstList && second is IList secondList)
        {
            return firstList.Count == secondList.Count &&
                   Enumerable.Range(0, firstList.Count)
                       .All(index => AreEqual(firstList[index], secondList[index]));
        }

        return first.Equals(second);
    }

    private static void RemoveDefaultDuplicates(
        Dictionary<string, object?> defaults,
        Dictionary<string, object?> target)
    {
        foreach (var key in target.Keys.ToList())
        {
            if (!defaults.TryGetValue(key, out var defaultValue))
                continue;

            var value = target[key];
            if (AreEqual(defaultValue, value))
            {
                target.Remove(key);
                continue;
            }

            if (defaultValue is Dictionary<string, object?> defaultMap &&
                value is Dictionary<string, object?> targetMap)
            {
                RemoveDefaultDuplicates(defaultMap, targetMap);
            }
        }
    }

    private static bool CanInitializeMembers(object instance, HashSet<Type> activeTypes)
    {
        var type = instance.GetType();
        if (!activeTypes.Add(type))
            return false;

        try
        {
            foreach (var member in GetWritableMembers(type))
            {
                if (member.GetValue(instance) != null)
                    continue;

                if (!CanCreateDefaultValue(member.Type, activeTypes))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            activeTypes.Remove(type);
        }
    }

    private static bool CanCreateDefaultValue(Type type, HashSet<Type> activeTypes)
    {
        if (type == typeof(object) || !type.IsClass && !type.IsInterface)
            return true;

        if (type == typeof(string))
            return true;

        if (IsConcreteCollectionLike(type))
            return TryCreateInstance(type) != null || type.IsArray;

        if (type.IsClass && !type.IsAbstract)
        {
            var instance = TryCreateInstance(type);
            return instance != null && !activeTypes.Contains(type) && CanInitializeMembers(instance, activeTypes);
        }

        var concrete = FindConcreteAssignableType(type);
        return concrete == null || !activeTypes.Contains(concrete) && CanCreateDefaultValue(concrete, activeTypes);
    }

    private static void InitializeMembers(object instance, HashSet<Type> activeTypes)
    {
        var type = instance.GetType();
        if (!activeTypes.Add(type))
            return;

        try
        {
            foreach (var member in GetWritableMembers(type))
            {
                if (member.GetValue(instance) != null || !TryCreateDefaultValue(member.Type, out var value) || value == null)
                    continue;

                try
                {
                    member.SetValue(instance, value);
                    if (value.GetType().IsClass && !IsConcreteCollectionLike(value.GetType()))
                        InitializeMembers(value, activeTypes);
                }
                catch
                {
                    // Some serialized members intentionally reject reflective assignment.
                }
            }
        }
        finally
        {
            activeTypes.Remove(type);
        }
    }

    private static bool TryCreateDefaultValue(Type type, out object? value)
    {
        value = null;

        if (type == typeof(string))
        {
            value = string.Empty;
            return true;
        }

        if (type.IsArray)
        {
            value = Array.CreateInstance(type.GetElementType()!, 0);
            return true;
        }

        if (IsConcreteCollectionLike(type))
        {
            value = TryCreateInstance(type);
            return value != null;
        }

        if (type.IsClass && !type.IsAbstract)
        {
            value = TryCreateInstance(type);
            return value != null;
        }

        if (type.IsInterface || type.IsAbstract)
        {
            var concrete = FindConcreteAssignableType(type);
            value = concrete == null ? null : TryCreateInstance(concrete);
            return value != null;
        }

        return false;
    }

    private static object? TryCreateInstance(Type type)
    {
        var constructor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);

        return constructor == null ? null : Activator.CreateInstance(type, nonPublic: true);
    }

    private static bool IsConcreteCollectionLike(Type type)
    {
        if (type.IsAbstract || type.IsInterface)
            return false;

        return typeof(IDictionary).IsAssignableFrom(type) ||
               typeof(IList).IsAssignableFrom(type) ||
               type.IsArray ||
               type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
    }

    private static Type? FindConcreteAssignableType(Type target)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(type => type != null).Cast<Type>().ToArray();
            }

            var candidate = types.FirstOrDefault(type =>
                !type.IsAbstract &&
                !type.IsInterface &&
                target.IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) != null);

            if (candidate != null)
                return candidate;
        }

        return null;
    }

    private static IEnumerable<SerializedMember> GetSerializedMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

        foreach (var field in type.GetFields(flags))
        {
            var attribute = field.GetCustomAttribute<DataFieldAttribute>();
            if (attribute != null)
                yield return new SerializedMember(field, attribute.Tag ?? LowerFirst(field.Name), field.FieldType, attribute);
        }

        foreach (var property in type.GetProperties(flags))
        {
            var attribute = property.GetCustomAttribute<DataFieldAttribute>();
            if (attribute != null && property.GetGetMethod(true) != null)
                yield return new SerializedMember(property, attribute.Tag ?? LowerFirst(property.Name), property.PropertyType, attribute);
        }
    }

    private static IEnumerable<SerializedMember> GetWritableMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var field in type.GetFields(flags))
        {
            if (!field.IsInitOnly)
                yield return new SerializedMember(field, string.Empty, field.FieldType, null);
        }

        foreach (var property in type.GetProperties(flags))
        {
            if (property.CanWrite && property.GetIndexParameters().Length == 0)
                yield return new SerializedMember(property, string.Empty, property.PropertyType, null);
        }
    }

    private static bool IsFlagsEnum(Type type) =>
        type.IsEnum && type.GetCustomAttribute<FlagsAttribute>(inherit: false) != null;

    private static bool IsSingleSetFlag(long flag, long value) =>
        flag != 0 && (flag & (flag - 1)) == 0 && (value & flag) != 0;

    private static string LowerFirst(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);

    private sealed class SerializedMember
    {
        internal SerializedMember(MemberInfo member, string tag, Type type, DataFieldAttribute? attribute)
        {
            Member = member;
            Tag = tag;
            Type = type;
            Attribute = attribute;
        }

        internal MemberInfo Member { get; }
        internal string Tag { get; }
        internal Type Type { get; }
        internal DataFieldAttribute? Attribute { get; }

        internal object? GetValue(object instance)
        {
            if (Member is FieldInfo field)
                return field.GetValue(instance);

            if (Member is PropertyInfo property)
                return property.GetValue(instance);

            return null;
        }

        internal void SetValue(object instance, object value)
        {
            if (Member is FieldInfo field)
            {
                field.SetValue(instance, value);
                return;
            }

            if (Member is PropertyInfo property)
                property.SetValue(instance, value);
        }
    }
}
