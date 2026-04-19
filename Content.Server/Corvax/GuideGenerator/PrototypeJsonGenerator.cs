using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.GuideGenerator;

public static class PrototypeJsonGenerator
{
    public static void PublishAll(IResourceManager res, ResPath destRoot)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>();
        var ser = IoCManager.Resolve<ISerializationManager>();

        foreach (var kind in proto.EnumeratePrototypeKinds().OrderBy(t => t.Name))
        {
            // The entity prototype has its own generator due to its size <see cref="EntityJsonGenerator"/>.
            if (kind == typeof(EntityPrototype))
                continue;

            if (HasUnsafeSerializedDataField(kind))
                continue;

            // Map: entity id -> prototype fields
            var map = new Dictionary<string, object?>();

            foreach (var p in proto.EnumeratePrototypes(kind))
            {
                var node = ser.WriteValueAs<MappingDataNode>(kind, p);
                node.Remove("id");
                map[p.ID] = FieldEntry.DataNodeToObject(node);
            }

            if (map.Count == 0)
                continue;

            // Determine default field for this prototype.
            object? defaultObj = null;
            try
            {
                var instance = Activator.CreateInstance(kind);
                if (instance != null)
                {
                    FieldEntry.EnsureFieldsCollectionsInitialized(instance);
                    var defaultNode = ser.WriteValueAs<MappingDataNode>(kind, instance, true);
                    defaultNode.Remove("id");
                    FieldEntry.NormalizeFlagsToSequences(instance, defaultNode);
                    defaultObj = FieldEntry.DataNodeToObject(defaultNode);
                }
            }
            catch
            {
                defaultObj = new Dictionary<string, object?>();
            }

            var outObj = new Dictionary<string, object?>
            {
                ["default"] = defaultObj,
                ["id"] = map
            };

            var serializeOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            res.UserData.CreateDir(destRoot);
            var kindName = proto.TryGetKindFrom(kind, out var actualKindName)
                ? actualKindName
                : kind.Name;
            var fileName = TextTools.DecapitalizeString(kindName) + ".json";
            var file = res.UserData.OpenWriteText(destRoot / fileName);
            file.Write(JsonSerializer.Serialize(outObj, serializeOptions));
            file.Flush();
        }
    }

    private static bool HasUnsafeSerializedDataField(Type type)
    {
        return HasUnsafeSerializedDataField(type, new HashSet<Type>());
    }

    private static bool HasUnsafeSerializedDataField(Type type, HashSet<Type> visited)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        if (!visited.Add(type))
            return false;

        foreach (var field in type.GetFields(flags))
        {
            if (!HasDataField(field))
                continue;

            if (IsUnsafeSerializedType(field.FieldType, visited))
                return true;
        }

        foreach (var property in type.GetProperties(flags))
        {
            if (!HasDataField(property))
                continue;

            if (IsUnsafeSerializedType(property.PropertyType, visited))
                return true;
        }

        return false;
    }

    private static bool HasDataField(MemberInfo member)
    {
        return member.GetCustomAttributes(inherit: true)
            .Any(attr => attr.GetType().Name is nameof(DataFieldAttribute) or nameof(IdDataFieldAttribute));
    }

    private static bool IsUnsafeSerializedType(Type type, HashSet<Type> visited)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(object) ||
            type == typeof(EntityUid) ||
            type == typeof(NetEntity))
        {
            return true;
        }

        if (type.IsPrimitive ||
            type.IsEnum ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(TimeSpan))
        {
            return false;
        }

        if (type.IsArray)
            return IsUnsafeSerializedType(type.GetElementType()!, visited);

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                if (IsUnsafeSerializedType(argument, visited))
                    return true;
            }
        }

        return type.GetCustomAttributes(inherit: true)
                   .Any(attr =>
                   attr.GetType().Name is nameof(DataDefinitionAttribute) or nameof(SerializableAttribute))
                && HasUnsafeSerializedDataField(type, visited);
    }
}
