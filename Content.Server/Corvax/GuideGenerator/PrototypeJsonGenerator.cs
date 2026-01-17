using System.Linq;
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
            var fileName = PrototypeUtility.CalculatePrototypeName(kind.Name) + ".json";
            var file = res.UserData.OpenWriteText(destRoot / fileName);
            file.Write(JsonSerializer.Serialize(outObj, serializeOptions));
            file.Flush();
        }
    }
}
