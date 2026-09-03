using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.Corvax.GuideGenerator;

public static class FieldEntry
{
    public const string PrototypeId = "id";
    public const string DefaultField = "default";
    public const string ComponentType = "type";
    public const string ComponentTypePrefix = "type:";

    private static readonly Regex DecimalValueRegex = new(@"^[+-]?\d+\.\d+$");

    public static object? ProcessNode(object instance, MappingDataNode node, MappingDataNode? composed = null)
    {
        NormalizeFlagsToSequences(instance, node);
        AddReadOnlyFields(instance.GetType(), node, composed);
        return ConvertNode(node);
    }

    public static object? ComputePrototypeDefault(Type type, ISerializationManager serializationManager)
    {
        return TryCreate(type) is { } instance
            ? SerializeDefault(instance, type, serializationManager, removeId: true)
            : null;
    }

    public static object? ComputeComponentDefault(
        string name,
        IComponentFactory factory,
        ISerializationManager serializationManager)
    {
        if (!factory.TryGetRegistration(name, out var registration))
            return null;

        try
        {
            var component = factory.GetComponent(registration.Type);
            return SerializeDefault(component, component.GetType(), serializationManager, removeId: false);
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    public static bool TryWriteValueAsMapping(
        ISerializationManager manager,
        Type type,
        object value,
        out MappingDataNode node,
        bool alwaysWrite = false)
    {
        try
        {
            node = manager.WriteValueAs<MappingDataNode>(type, value, alwaysWrite);
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
        if (defaultObject is Dictionary<string, object?> defaults)
        {
            foreach (var fields in values.Values.OfType<Dictionary<string, object?>>())
            {
                RemoveDefaults(defaults, fields);
            }
        }

        return new Dictionary<string, object?> { [DefaultField] = defaultObject, [PrototypeId] = values };
    }

    public static object? ConvertNode(DataNode node)
    {
        return node switch
        {
            MappingDataNode mapping => ConvertMapping(mapping),
            SequenceDataNode sequence => ConvertSequence(sequence),
            ValueDataNode value => ConvertValue(value),
            _ => node.ToString()
        };
    }

    private static object ConvertMapping(MappingDataNode node)
    {
        var result = node.ToDictionary(pair => pair.Key, pair => ConvertNode(pair.Value));
        return node.Tag == null ? result : new Dictionary<string, object?> { [node.Tag] = result };
    }

    private static object ConvertSequence(SequenceDataNode node)
    {
        var values = node.Select(ConvertNode).ToList();
        if (values.Any(value => value is not Dictionary<string, object?> map || !map.ContainsKey(ComponentType)))
            return values;

        var result = new Dictionary<string, object?>();
        foreach (var value in values.Cast<Dictionary<string, object?>>())
        {
            var type = value[ComponentType];
            result[$"{ComponentTypePrefix}{type}"] = value
                .Where(pair => pair.Key != ComponentType)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        return result;
    }

    public static Type GetMemberType(MemberInfo member)
    {
        return member switch
        {
            FieldInfo field => field.FieldType,
            PropertyInfo property => property.PropertyType,
            _ => throw new ArgumentException("Unsupported member type", nameof(member))
        };
    }

    public static void NormalizeFlagsToSequences(object instance, MappingDataNode node)
    {
        foreach (var key in node.Keys.ToArray())
        {
            var member = SerializedMembers(instance.GetType()).FirstOrDefault(m => string.Equals(m.Tag, key, StringComparison.OrdinalIgnoreCase));
            if (member == null || !member.Type.IsEnum ||
                member.Type.GetCustomAttribute<FlagsAttribute>() == null || member.Get(instance) is not { } value)
                continue;

            var number = Convert.ToInt64(value);
            var flags = Enum.GetValues(member.Type)
                .Cast<object>()
                .Where(flag =>
                {
                    var numberFlag = Convert.ToInt64(flag);
                    return numberFlag != 0 && (numberFlag & (numberFlag - 1)) == 0 && (number & numberFlag) != 0;
                })
                .Select(flag => Enum.GetName(member.Type, flag)!);
            node[key] = new SequenceDataNode(flags.ToArray());
        }
    }

    private static object? SerializeDefault(
        object instance,
        Type type,
        ISerializationManager manager,
        bool removeId)
    {
        try
        {
            EnsureCollections(instance);
            if (!TryWriteValueAsMapping(manager, type, instance, out var node, alwaysWrite: true))
                return new Dictionary<string, object?>();

            if (removeId)
                node.Remove(PrototypeId);

            NormalizeFlagsToSequences(instance, node);
            return ConvertNode(node);
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

    private static void AddReadOnlyFields(Type type, MappingDataNode target, MappingDataNode? composed)
    {
        if (composed == null)
            return;

        foreach (var member in SerializedMembers(type))
        {
            if (!member.Attribute.ReadOnly || target.ContainsKey(member.Tag) || !composed.TryGetValue(member.Tag, out var value))
                continue;

            target[member.Tag] = value.Copy();
        }
    }

    private static object? ConvertValue(ValueDataNode node)
    {
        if (node.IsNull)
            return null;

        if (string.IsNullOrEmpty(node.Value))
            return node.Tag == null ? string.Empty : new Dictionary<string, object?> { [node.Tag] = new Dictionary<string, object?>() };

        object value;
        if (bool.TryParse(node.Value, out var boolean))
        {
            value = boolean;
        }
        else if (int.TryParse(node.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            value = integer;
        }
        else if (DecimalValueRegex.IsMatch(node.Value) &&
                 double.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            value = number;
        }
        else
        {
            value = node.Value;
        }

        return node.Tag == null ? value : new Dictionary<string, object?> { [node.Tag] = value };
    }

    private static void RemoveDefaults(Dictionary<string, object?> defaults, Dictionary<string, object?> values)
    {
        foreach (var key in values.Keys.ToArray())
        {
            if (!defaults.TryGetValue(key, out var defaultValue))
                continue;

            if (Equal(defaultValue, values[key]))
            {
                values.Remove(key);
            }
            else if (defaultValue is Dictionary<string, object?> defaultMap &&
                     values[key] is Dictionary<string, object?> valueMap)
            {
                RemoveDefaults(defaultMap, valueMap);
            }
        }
    }

    private static bool Equal(object? first, object? second)
    {
        if (first is IDictionary<string, object?> firstMap &&
            second is IDictionary<string, object?> secondMap)
        {
            return firstMap.Count == secondMap.Count &&
                   firstMap.All(pair => secondMap.TryGetValue(pair.Key, out var value) && Equal(pair.Value, value));
        }

        if (first is IList firstList && second is IList secondList)
        {
            return firstList.Count == secondList.Count &&
                   Enumerable.Range(0, firstList.Count).All(i => Equal(firstList[i], secondList[i]));
        }

        return Equals(first, second);
    }

    // The serializer omits null object graphs, so initialize only serialized fields.
    // Runtime fields such as ItemSlotsComponent.Slots must come from composition.
    private static void EnsureCollections(object instance)
    {
        foreach (var member in SerializedMembers(instance.GetType()))
        {
            if (member.Get(instance) != null || !member.Type.IsClass || member.Type == typeof(string) ||
                member.Type.IsAbstract || member.Type.GetConstructor(Type.EmptyTypes) == null)
                continue;
            try
            {
                if (Activator.CreateInstance(member.Type) is { } value)
                    member.Set(instance, value);
            }
            catch
            {
                // Some serializers intentionally expose read-only runtime members.
            }
        }
    }

    private static object? TryCreate(Type type)
    {
        var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        return constructor == null ? null : Activator.CreateInstance(type, true);
    }

    private static readonly ConcurrentDictionary<Type, SerializedMember[]> MemberCache = new();

    private static IEnumerable<SerializedMember> SerializedMembers(Type type)
    {
        return MemberCache.GetOrAdd(type, FindSerializedMembers);
    }

    private static SerializedMember[] FindSerializedMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var members = new List<SerializedMember>();
        foreach (var field in type.GetFields(flags))
        {
            if (field.GetCustomAttribute<DataFieldAttribute>() is { } attribute)
                members.Add(new SerializedMember(field, attribute, attribute.Tag ?? LowerFirst(field.Name), field.FieldType));
        }

        foreach (var property in type.GetProperties(flags))
        {
            if (property.GetCustomAttribute<DataFieldAttribute>() is { } attribute && property.GetGetMethod(true) != null)
                members.Add(new SerializedMember(property, attribute, attribute.Tag ?? LowerFirst(property.Name), property.PropertyType));
        }

        return [.. members];
    }

    private static string LowerFirst(string value)
    {
        return string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
    }

    private sealed class SerializedMember(
        MemberInfo info,
        DataFieldAttribute attribute,
        string tag,
        Type type)
    {
        public readonly DataFieldAttribute Attribute = attribute;
        public readonly string Tag = tag;
        public readonly Type Type = type;
        public object? Get(object instance)
        {
            return info switch
            {
                FieldInfo field => field.GetValue(instance),
                PropertyInfo property => property.GetValue(instance),
                _ => null
            };
        }

        public void Set(object instance, object value)
        {
            switch (info)
            {
                case FieldInfo { IsInitOnly: false } field:
                    field.SetValue(instance, value);
                    break;
                case PropertyInfo { CanWrite: true } property:
                    property.SetValue(instance, value);
                    break;
            }
        }
    }
}
