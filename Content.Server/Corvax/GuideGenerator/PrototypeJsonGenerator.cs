using System.Linq;
using System.Reflection;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.GuideGenerator;

public static class PrototypeJsonGenerator
{
    public static void PublishAll(IResourceManager res, ResPath destination)
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var serializationManager = IoCManager.Resolve<ISerializationManager>();
        var componentFactory = IoCManager.Resolve<IComponentFactory>();
        var destinationRoot = new ResPath("prototype").ToRootedPath();

        foreach (var kind in prototypeManager.EnumeratePrototypeKinds().OrderBy(type => type.Name))
        {
            // The entity prototype has its own generator due to its size <see cref="EntityJsonGenerator"/>.
            var isEntityPrototype = kind == typeof(EntityPrototype);

            if (HasUnsafeSerializedDataField(kind, new HashSet<Type>()))
                continue;

            // Map: entity id -> prototype fields
            var map = new Dictionary<string, object?>();
            foreach (var prototype in prototypeManager.EnumeratePrototypes(kind))
            {
                if (!FieldEntry.TryWriteValueAsMapping(serializationManager, kind, prototype, out var node))
                    continue;

                node.Remove(FieldEntry.PrototypeId);
                var fields = FieldEntry.ProcessNode(prototype, node);
                if (isEntityPrototype && prototype is EntityPrototype entityPrototype)
                    fields = ProcessEntityPrototype(entityPrototype, prototypeManager, serializationManager, componentFactory, fields);

                map[prototype.ID] = fields;
            }

            if (map.Count == 0)
                continue;

            var defaultObject = FieldEntry.ComputePrototypeDefault(kind, serializationManager);
            var output = FieldEntry.DeduplicateAgainstDefault(defaultObject, map);
            res.UserData.CreateDir(destinationRoot);

            var kindName = prototypeManager.TryGetKindFrom(kind, out var actualKindName)
                ? actualKindName
                : kind.Name;
            var directoryName = TextTools.CapitalizeString(kindName);

            if (!isEntityPrototype)
            {
                GuideJson.WriteFile(res, destinationRoot / $"{directoryName}.json", output);
                continue;
            }

            var entityRoot = destinationRoot / directoryName;
            res.UserData.CreateDir(entityRoot);
            var entityPrototypes = output.TryGetValue(FieldEntry.PrototypeId, out var idValue) && idValue is Dictionary<string, object?> em
                ? em
                : output;

            foreach (var (id, fields) in entityPrototypes)
            {
                GuideJson.WriteFile(res, entityRoot / $"{id}.json", fields);
            }
        }
    }

    private static object? ProcessEntityPrototype(EntityPrototype entProto, IPrototypeManager proto, ISerializationManager ser, IComponentFactory compFactory, object? fields)
    {
        if (fields is not Dictionary<string, object?> fieldMap)
            return fields;

        var componentMap = ComponentJsonGenerator.BuildEntityComponentMap(entProto, proto, ser, compFactory);
        if (componentMap.Count == 0)
            fieldMap.Remove("components");
        else
            fieldMap["components"] = componentMap;

        return fieldMap;
    }

    private static bool HasUnsafeSerializedDataField(Type type, HashSet<Type> visited)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        if (!visited.Add(type))
            return false;

        return type.GetFields(flags)
            .Cast<MemberInfo>()
            .Concat(type.GetProperties(flags))
            .Any(m => HasDataField(m) && IsUnsafeSerializedType(FieldEntry.GetMemberType(m), visited));
    }

    private static bool HasDataField(MemberInfo member)
    {
        return member.GetCustomAttributes(inherit: true)
            .Any(attr => attr.GetType().Name is nameof(DataFieldAttribute) or nameof(IdDataFieldAttribute));
    }

    private static bool IsUnsafeSerializedType(Type type, HashSet<Type> visited)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(EntityUid) || type == typeof(NetEntity))
            return true;

        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(TimeSpan))
            return false;

        if (type.IsArray)
            return IsUnsafeSerializedType(type.GetElementType()!, visited);

        if (type.IsGenericType)
            return type.GetGenericArguments().Any(arg => IsUnsafeSerializedType(arg, visited));

        return type.GetCustomAttributes(inherit: true)
                   .Any(attr => attr.GetType().Name is nameof(DataDefinitionAttribute) or nameof(SerializableAttribute))
               && HasUnsafeSerializedDataField(type, visited);
    }
}
