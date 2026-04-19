using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Content.Shared.Labels.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Corvax.GuideGenerator;

public sealed class EntityJsonGenerator
{
    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("desc")]
    public string Description { get; }

    [JsonPropertyName("suffix")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Suffix { get; }

    [JsonPropertyName("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; }

    public EntityJsonGenerator(EntityPrototype proto)
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var loc = IoCManager.Resolve<ILocalizationManager>();

        Id = proto.ID;
        Name = TextTools.CapitalizeString(TextTools.GetDisplayName(proto, prototypeManager, loc));
        Description = proto.Description;

        if (!string.IsNullOrWhiteSpace(proto.EditorSuffix))
            Suffix = TextTools.GetEditorSuffix(proto.EditorSuffix, EntityNameDuplicatesJsonGenerator.IgnoredSuffixTokens, TextTools.NormalizeSuffixToken);

        Label = proto.Components.Values
            .Select(x => x.Component)
            .OfType<LabelComponent>()
            .Select(lc => lc.CurrentLabel)
            .Where(label => !string.IsNullOrEmpty(label))
            .Select(label => Loc.GetString(label!))
            .FirstOrDefault();
    }

    public static void PublishJson(StreamWriter file)
    {
        var prototype = IoCManager.Resolve<IPrototypeManager>();
        var prototypes =
            prototype
                .EnumeratePrototypes<EntityPrototype>()
                .Where(x => !x.Abstract)
                .Select(x => new EntityJsonGenerator(x))
                .ToDictionary(x => x.Id, x => x);

        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        file.Write(JsonSerializer.Serialize(prototypes, serializeOptions));
    }
}
