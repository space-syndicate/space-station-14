using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.GuideGenerator;

internal static class GuideDataGenerator
{
    internal static void Generate(IResourceManager resourceManager, ResPath destination)
    {
        WriteFile(resourceManager, destination, "entity_prototypes.json", EntityJsonGenerator.PublishJson);
        WriteFile(resourceManager, destination, "entity_parent.json", EntityParentJsonGenerator.PublishJson);
        WriteFile(resourceManager, destination, "loc.json", LocJsonGenerator.PublishJson);
        WriteFile(resourceManager, destination, "meta_license.json", MetaLicenseGenerator.PublishJson);
        WriteFile(resourceManager, destination, "prototype.json", PrototypeListGenerator.PublishJson);
        WriteFile(resourceManager, destination, "component.json", ComponentListGenerator.PublishJson);
        WriteFile(resourceManager, destination, "prototype_store.json", PrototypeStoreGenerator.PublishJson);
        WriteFile(resourceManager, destination, "component_store.json", ComponentStoreGenerator.PublishJson);
        WriteFile(resourceManager, destination, "entity_project.json", EntityProjectGenerator.PublishJson);
        WriteFile(resourceManager, destination, "entity_name.json", EntityNameDuplicatesJsonGenerator.PublishNameJson);
        WriteFile(resourceManager, destination, "entity_name_wiki.json", stream => WikiEntityNameGenerator.PublishJson(stream, resourceManager, destination));
        WriteFile(resourceManager, destination, "entity_name_duplicates.json", EntityNameDuplicatesJsonGenerator.PublishDuplicatesJson);
        WriteFile(resourceManager, destination, "tag.json", TagJsonGenerator.PublishJson);

        PrototypeJsonGenerator.PublishAll(resourceManager, destination);
        ComponentJsonGenerator.PublishAll(resourceManager, destination);
    }

    private static void WriteFile(
        IResourceManager resourceManager,
        ResPath destination,
        string name,
        Action<Stream> write)
    {
        using var stream = resourceManager.UserData.OpenWrite(destination.WithName(name));
        write(stream);
    }
}

internal static class GuideJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static void Write(Stream stream, object? value) => JsonSerializer.Serialize(stream, value, Options);

    internal static void WriteFile(IResourceManager resources, ResPath path, object? value)
    {
        using var stream = resources.UserData.OpenWrite(path);
        Write(stream, value);
    }
}
