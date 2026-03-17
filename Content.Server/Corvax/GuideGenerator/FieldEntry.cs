using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server.Corvax.GuideGenerator;

public static class FieldEntry
{
    private static readonly Regex DoubleEntryRegex = new(@"^[+-]?\d+\.\d+$");

    public static object? DataNodeToObject(DataNode node)
    {
        if (node is MappingDataNode mapping)
        {
            var dict = new Dictionary<string, object?>();

            foreach (var kv in mapping)
            {
                dict[kv.Key] = DataNodeToObject(kv.Value);
            }

            if (node.Tag != null)
            {
                var wrapped = new Dictionary<string, object?>
                {
                    [node.Tag] = dict
                };
                return wrapped;
            }

            return dict;
        }

        if (node is SequenceDataNode sequence)
        {
            var items = new List<object?>();
            foreach (var item in sequence)
            {
                items.Add(DataNodeToObject(item));
            }

            var typedMap = new Dictionary<string, object?>();
            var canRewrite = true;
            foreach (var obj in items)
            {
                if (obj is not Dictionary<string, object?> dict ||
                    !dict.TryGetValue("type", out var typeVal) ||
                    typeVal is null)
                {
                    canRewrite = false;
                    break;
                }

                var key = $"type:{typeVal}";
                var cloned = new Dictionary<string, object?>(dict);
                cloned.Remove("type");
                typedMap[key] = cloned;
            }

            if (canRewrite && typedMap.Count > 0)
                return typedMap;

            return items;
        }

        if (node is ValueDataNode value)
        {
            if (value.IsNull)
                return null;

            var raw = value.Value;

            if (bool.TryParse(raw, out var boolRes))
                return boolRes;

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intRes))
                return intRes;

            if (DoubleEntryRegex.IsMatch(raw) &&
                double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleRes))
                return doubleRes;

            return raw;
        }

        return node.ToString();
    }

    public static void NormalizeFlagsToSequences(object instance, MappingDataNode node)
    {
        var type = instance.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

        foreach (var key in node.Keys.ToList())
        {
            var prop = type.GetProperty(key, flags);
            MemberInfo? member = prop != null && prop.GetGetMethod(true) != null ? prop : type.GetField(key, flags);
            if (member == null)
                continue;

            var memberType = member is PropertyInfo p ? p.PropertyType : ((FieldInfo)member).FieldType;
            if (!memberType.IsEnum)
                continue;

            if (memberType.GetCustomAttribute<FlagsAttribute>(false) == null)
                continue;

            var value = member is PropertyInfo p2 ? p2.GetValue(instance) : ((FieldInfo)member).GetValue(instance);
            if (value == null)
                continue;

            var intVal = Convert.ToInt64(value);
            var names = new List<string>();
            foreach (var v in Enum.GetValues(memberType))
            {
                var i = Convert.ToInt64(v);
                if (i == 0)
                    continue;
                if ((i & (i - 1)) == 0 && (intVal & i) != 0)
                    names.Add(Enum.GetName(memberType, v)!);
            }

            node[key] = new SequenceDataNode(names.ToArray());
        }
    }

    public static void EnsureFieldsCollectionsInitialized(object instance)
    {
        var type = instance.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Fields
        foreach (var field in type.GetFields(flags))
        {
            if (field.IsInitOnly)
                continue;

            try
            {
                var value = field.GetValue(instance);
                if (value != null)
                    continue;

                var ft = field.FieldType;
                if (ft == typeof(string))
                {
                    field.SetValue(instance, string.Empty);
                }
                else if ((typeof(IDictionary).IsAssignableFrom(ft) || typeof(IList).IsAssignableFrom(ft) || ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>) || ft.IsArray) && ft is { IsAbstract: false, IsInterface: false })
                {
                    object? created = null;
                    if (ft.IsArray)
                    {
                        var elemType = ft.GetElementType();
                        if (elemType != null)
                            created = Array.CreateInstance(elemType, 0);
                    }
                    else if (ft.GetConstructor(Type.EmptyTypes) != null)
                    {
                        created = Activator.CreateInstance(ft);
                    }

                    if (created != null)
                        field.SetValue(instance, created);
                }
                else if (ft.IsClass && ft != typeof(string) && !ft.IsAbstract)
                {
                    var created = Activator.CreateInstance(ft, true);
                    if (created != null)
                    {
                        field.SetValue(instance, created);
                        EnsureFieldsCollectionsInitialized(created);
                    }
                }
                else if ((ft.IsAbstract || ft.IsInterface) && ft != typeof(string))
                {
                    var concrete = FindConcreteAssignableType(ft);
                    if (concrete != null)
                    {
                        var created = Activator.CreateInstance(concrete);
                        if (created != null)
                        {
                            field.SetValue(instance, created);
                            EnsureFieldsCollectionsInitialized(created);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        // Properties
        foreach (var prop in type.GetProperties(flags))
        {
            if (!prop.CanWrite || prop.GetIndexParameters().Length != 0)
                continue;

            try
            {
                var value = prop.GetValue(instance);
                if (value != null)
                    continue;

                var pt = prop.PropertyType;
                if ((typeof(IDictionary).IsAssignableFrom(pt) || typeof(IList).IsAssignableFrom(pt) || pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(List<>) || pt.IsArray) && pt is { IsAbstract: false, IsInterface: false })
                {
                    object? created = null;
                    if (pt.IsArray)
                    {
                        var elemType = pt.GetElementType();
                        if (elemType != null)
                            created = Array.CreateInstance(elemType, 0);
                    }
                    else if (pt.GetConstructor(Type.EmptyTypes) != null)
                    {
                        created = Activator.CreateInstance(pt);
                    }

                    if (created != null)
                        prop.SetValue(instance, created);
                }
                else if (pt.IsClass && pt != typeof(string) && !pt.IsAbstract)
                {
                    var created = Activator.CreateInstance(pt, true);
                    if (created != null)
                    {
                        prop.SetValue(instance, created);
                        EnsureFieldsCollectionsInitialized(created);
                    }
                }
                else if ((pt.IsAbstract || pt.IsInterface) && pt != typeof(string))
                {
                    var concrete = FindConcreteAssignableType(pt);
                    if (concrete != null)
                    {
                        var created = Activator.CreateInstance(concrete);
                        if (created != null)
                        {
                            prop.SetValue(instance, created);
                            EnsureFieldsCollectionsInitialized(created);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    private static Type? FindConcreteAssignableType(Type target)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var types = asm.GetTypes();

            foreach (var t in types)
            {
                if (t.IsAbstract || t.IsInterface)
                    continue;
                if (!target.IsAssignableFrom(t))
                    continue;
                if (t.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                return t;
            }
        }

        return null;
    }
}
