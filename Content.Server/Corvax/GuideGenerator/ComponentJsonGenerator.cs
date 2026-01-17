using System.Text.Encodings.Web;
using System.Text.Json;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.GuideGenerator;

public static class ComponentJsonGenerator
{
    public static void PublishAll(IResourceManager res, ResPath destRoot)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>();
        var ser = IoCManager.Resolve<ISerializationManager>();
        var compFactory = IoCManager.Resolve<IComponentFactory>();
        var entMan = IoCManager.Resolve<IEntityManager>();

        // Map: component name -> (entity id -> component fields)
        var output = new Dictionary<string, Dictionary<string, object?>>();

        foreach (var p in proto.EnumeratePrototypes(typeof(EntityPrototype)))
        {
            if (p is not EntityPrototype entProto)
                continue;

            foreach (var (compName, entry) in entProto.Components)
            {
                var node = ser.WriteValueAs<MappingDataNode>(entry.Component.GetType(), entry.Component);
                var compFields = FieldEntry.DataNodeToObject(node);

                if (!output.TryGetValue(compName, out var map))
                {
                    map = new Dictionary<string, object?>();
                    output[compName] = map;
                }

                map[entProto.ID] = compFields;
            }
        }

        if (output.Count == 0)
            return;

        foreach (var (compName, map) in output)
        {
            // Determine default field for this component.
            object? defaultObj = null;
            if (compFactory.TryGetRegistration(compName, out var registration))
            {
                var uid = entMan.CreateEntityUninitialized(null);
                try
                {
                    var compInstance = compFactory.GetComponent(registration.Type);
                    FieldEntry.EnsureFieldsCollectionsInitialized(compInstance);
                    entMan.AddComponent(uid, compInstance);
                    var node = ser.WriteValueAs<MappingDataNode>(compInstance.GetType(), compInstance, true);
                    defaultObj = FieldEntry.DataNodeToObject(node);
                }
                catch
                {
                    defaultObj = new Dictionary<string, object?>();
                }
                finally
                {
                    entMan.DeleteEntity(uid);
                }
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
            var fileName = PrototypeUtility.CalculatePrototypeName(compName) + ".json";
            var file = res.UserData.OpenWriteText(destRoot / fileName);
            file.Write(JsonSerializer.Serialize(outObj, serializeOptions));
            file.Flush();
        }
    }
}
